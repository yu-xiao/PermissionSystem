using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using PermissionSystem.AiEvaluations;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiScenarioEvaluationVerifier
{
    public const int MaxReportBytes = 32 * 1024 * 1024;
    private static readonly Lazy<EvaluationSuite> Suite = new(() =>
    {
        using var stream = typeof(AiScenarioEvaluationVerifier).Assembly.GetManifestResourceStream("AiScenarioCases.json")
            ?? throw new InvalidOperationException("The build has no evaluation expectations.");
        var suite = JsonSerializer.Deserialize<EvaluationSuite>(stream, EvaluationJson.Options)!;
        suite.Validate();
        return suite;
    });

    public EvaluationReport Verify(string json, AiScenarioSnapshot snapshot, string hash)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxReportBytes)
            Invalid("Evaluation report is empty or too large.");
        try
        {
            // Reject duplicate properties before typed deserialization can silently keep their last value.
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 48 });
            CheckProperties(document.RootElement);
            var report = JsonSerializer.Deserialize<EvaluationReport>(json, EvaluationJson.Options) ?? throw new JsonException();
            if (report.SnapshotHash != hash || report.BuildIdentity != snapshot.BuildIdentity ||
                report.SuiteHash != EvaluationJson.Digest(JsonSerializer.Serialize(Suite.Value, EvaluationJson.Options)) ||
                report.CheckerVersion != "1.0" || report.Results.Count != 41 || report.Manifest.Count != 41 ||
                report.MaxTokens != snapshot.Configuration.MaxTokens || report.Temperature != snapshot.Configuration.Temperature ||
                !Enum.IsDefined(report.Mode) || report.StartedAt == default || report.StartedAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
                report.Mode == EvaluationMode.Live && (report.ModelFingerprint?.Length != 64 || report.Model == "scripted"))
                Invalid("Report is not bound to this exact snapshot, build, model configuration and approved suite.");
            if (EvaluationRedactor.Sanitize(report).Changed) Invalid("Report requires redaction or truncation; sanitize it before importing.");
            var expected = Suite.Value.Cases.SelectMany(c => c.Variants.Select(v => new
            {
                Key = c.Id + "/" + v.Id, Hash = EvaluationJson.Digest(JsonSerializer.Serialize(new
                    { c.Id, c.Version, c.Name, c.Category, c.SafetyCritical, Variant = v }, EvaluationJson.Options)),
                c.SafetyCritical, Variant = v
            })).ToArray();
            if (report.Results.Select(r => r.Key).Distinct().Count() != expected.Length ||
                report.Manifest.Select(m => m.Key).Distinct().Count() != expected.Length)
                Invalid("Duplicated or missing evaluation cases.");
            foreach (var item in expected)
            {
                var result = report.Results.SingleOrDefault(r => r.Key == item.Key);
                var manifest = report.Manifest.SingleOrDefault(m => m.Key == item.Key);
                if (result is null || manifest is null || result.CaseHash != item.Hash || manifest.CaseHash != item.Hash ||
                    result.SafetyCritical != item.SafetyCritical || manifest.SafetyCritical != item.SafetyCritical ||
                    result.LiveApplicable != item.Variant.LiveApplicable || manifest.LiveApplicable != item.Variant.LiveApplicable ||
                    manifest.StepCount != item.Variant.Steps.Count || result.Steps.Count > item.Variant.Steps.Count)
                    Invalid("Case expectations or coverage do not match the build's fixed suite.");
                if (report.Mode == EvaluationMode.Live && !item.Variant.LiveApplicable)
                {
                    if (result!.Steps.Count != 0 || result.Status != EvaluationStatus.NotApplicable) Invalid("Invalid live applicability.");
                    continue;
                }
                for (var i = 0; i < result!.Steps.Count; i++)
                {
                    var observation = result.Steps[i];
                    if (observation.Input != item.Variant.Steps[i].Input || observation.Models.Count > 6 || observation.Tools.Count > 10 ||
                        observation.Evidence.Count > 10 || observation.Usage.Count > 6) Invalid("Invalid bounded observation.");
                    foreach (var model in observation.Models)
                    {
                        if (model.BasePromptHash != EvaluationJson.Digest(snapshot.SystemPrompt) ||
                            model.Temperature != snapshot.Configuration.Temperature || model.MaxTokens != snapshot.Configuration.MaxTokens ||
                            model.TimeoutSeconds != report.TimeoutSeconds || model.RequestedModel != report.Model ||
                            model.Tools.Count > snapshot.Tools.Length || model.Tools.Select(t => t.Name).Distinct().Count() != model.Tools.Count)
                            Invalid("Observed request did not execute the candidate prompt or model parameters.");
                        foreach (var tool in model.Tools)
                        {
                            var definition = snapshot.Tools.SingleOrDefault(t => t.FunctionName == tool.Name);
                            if (definition is null || tool.ToolCode != definition.ToolCode || tool.Version != definition.Version ||
                                tool.DescriptionHash != EvaluationJson.Digest(definition.Description) ||
                                tool.InputSchemaHash != EvaluationJson.Digest(definition.ModelSchemaJson) ||
                                tool.OutputSchemaHash != EvaluationJson.Digest(definition.OutputSchemaJson))
                                Invalid("Observed tool contract differs from this snapshot.");
                        }
                    }
                    observation.Checks = EvaluationChecker.Check(item.Variant.Steps[i].ScenarioExpected ?? item.Variant.Steps[i].Expected, observation);
                }
                result.Status = result.Steps.Count != item.Variant.Steps.Count ? EvaluationStatus.NotExecuted :
                    result.Steps.All(s => s.Checks.All(c => c.Passed)) ? report.Mode == EvaluationMode.Live
                        ? EvaluationStatus.PendingReview : EvaluationStatus.Passed : EvaluationStatus.Failed;
            }
            var modelObservations = report.Results.SelectMany(r => r.Steps).SelectMany(s => s.Models).ToArray();
            if (report.CallCount != modelObservations.Length || report.AccountedTokens < modelObservations.Sum(m => (long)m.ReservedTokens) ||
                report.AccountedCost != modelObservations.Sum(m => m.AccountedCost) ||
                modelObservations.Any(m => m.ReservedTokens <= 0 || m.AccountedCost <= 0) ||
                report.Mode == EvaluationMode.Live && (report.Limits is null || report.Limits.MaxCalls <= 0 || report.Limits.MaxTokens <= 0 ||
                    report.Limits.MaxEstimatedCost <= 0 || report.InputPricePerMillion is not > 0 || report.OutputPricePerMillion is not > 0 ||
                    string.IsNullOrWhiteSpace(report.Currency)))
                Invalid("Evaluation accounting is missing or inconsistent with the observed requests.");
            if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(report, EvaluationJson.Options)) > MaxReportBytes)
                Invalid("Normalized report is too large.");
            return report;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NullReferenceException or ArgumentException)
        { throw new BusinessException(ErrorCode.ValidationFailed, "Malformed evaluation report or observations."); }
    }

    private static void CheckProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in value.EnumerateObject())
            {
                if (!names.Add(p.Name)) throw new JsonException();
                CheckProperties(p.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) CheckProperties(item);
    }
    [DoesNotReturn]
    private static void Invalid(string message) => throw new BusinessException(ErrorCode.ValidationFailed, message);
}
