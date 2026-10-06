using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PermissionSystem.Application.AiCenter;

namespace PermissionSystem.AiEvaluations;

public static class EvaluationRunner
{
    public static async Task<EvaluationReport> RunAsync(string root, string suitePath,
        IAiModelGateway? liveGateway = null, LiveEvaluationSettings? live = null, string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        if ((liveGateway is null) != (live is null) || live is not null && string.IsNullOrWhiteSpace(apiKey))
            throw new EvaluationInputException("Live gateway, configuration and process credential must be provided together.");
        live?.Validate();
        var suite = EvaluationJson.Read<EvaluationSuite>(suitePath);
        suite.Validate();
        var budget = new EvaluationBudget(live?.Budget ?? new(1000, 100_000_000, 1000), live?.InputPricePerMillion ?? 1, live?.OutputPricePerMillion ?? 1);
        var source = Path.Combine(root, "backend", "PermissionSystem.AiEvaluations");
        var report = new EvaluationReport
        {
            SuiteHash = EvaluationJson.Digest(JsonSerializer.Serialize(suite, EvaluationJson.Options)),
            FixtureHash = SourceDigest(root, [Path.Combine(source, "IsolatedEvaluationEnvironment.cs"), Path.Combine(source, "EvaluationGateway.cs")]),
            CheckerHash = SourceDigest(root, [Path.Combine(source, "EvaluationChecker.cs"), Path.Combine(source, "EvaluationModels.cs"), Path.Combine(source, "EvaluationRedactor.cs")]),
            SourceHash = SourceDigest(root, Directory.GetFiles(Path.Combine(root, "backend"), "*", SearchOption.AllDirectories)
                .Where(p => Path.GetExtension(p) is ".cs" or ".csproj" or ".sln")
                .Where(p => !p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s is "bin" or "obj"))),
            GitCommit = Git(root, ["rev-parse", "HEAD"]), WorkingTreeDirty = !string.IsNullOrEmpty(Git(root, ["status", "--porcelain"])),
            Mode = live is null ? EvaluationMode.Offline : EvaluationMode.Live, StartedAt = DateTimeOffset.UtcNow,
            ProviderAlias = live?.ProviderAlias ?? "offline", Model = live?.Model ?? "scripted", Temperature = live?.Temperature ?? 0,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + "; EF Core " + typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.GetName().Version,
            ComplianceReference = live?.ComplianceReference,
            MaxTokens = live?.MaxTokens ?? 2048, TimeoutSeconds = live?.TimeoutSeconds ?? 30,
            Currency = live?.Currency ?? "XXX", InputPricePerMillion = live?.InputPricePerMillion ?? 1,
            OutputPricePerMillion = live?.OutputPricePerMillion ?? 1, Limits = live?.Budget ?? new(1000, 100_000_000, 1000)
        };
        foreach (var item in suite.Cases)
            foreach (var variant in item.Variants)
            {
                var result = new CaseResult
                {
                    Key = $"{item.Id}/{variant.Id}", CaseHash = EvaluationJson.Digest(JsonSerializer.Serialize(new { item.Id, item.Version, item.Name, item.Category, item.SafetyCritical, Variant = variant }, EvaluationJson.Options)),
                    SafetyCritical = item.SafetyCritical, LiveApplicable = variant.LiveApplicable, Status = EvaluationStatus.NotExecuted
                };
                report.Results.Add(result);
                report.Manifest.Add(new(result.Key, result.CaseHash, variant.Steps.Count, item.SafetyCritical, variant.LiveApplicable));
                if (live is not null && !variant.LiveApplicable)
                { result.Status = EvaluationStatus.NotApplicable; result.Reason = "Offline supplier or context injection; no real model capability claim."; continue; }
                if (budget.Stopped || cancellationToken.IsCancellationRequested)
                { result.Reason = "Budget exhausted or evaluation cancelled."; continue; }
                try
                {
                    await using var environment = await IsolatedEvaluationEnvironment.CreateAsync(variant.Fixture, budget, liveGateway, live, apiKey, cancellationToken);
                    foreach (var step in variant.Steps)
                    {
                        if (budget.Stopped || cancellationToken.IsCancellationRequested) break;
                        var observed = await environment.ExecuteAsync(step, cancellationToken);
                        observed.Checks = EvaluationChecker.Check(step.Expected, observed);
                        result.Steps.Add(observed);
                    }
                    if (result.Steps.Count != variant.Steps.Count) { result.Status = EvaluationStatus.NotExecuted; result.Reason = "Step coverage incomplete."; }
                    else result.Status = result.Steps.All(s => s.Checks.All(c => c.Passed))
                        ? live is null ? EvaluationStatus.Passed : EvaluationStatus.PendingReview : EvaluationStatus.Failed;
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    result.Status = EvaluationStatus.Failed;
                    result.Reason = exception is OperationCanceledException ? "Evaluation cancelled." : "Evaluation fixture or execution failed; no raw exception retained.";
                }
                var sanitized = EvaluationRedactor.Sanitize(result, apiKey);
                var safeResult = JsonSerializer.Deserialize<CaseResult>(sanitized.Json, EvaluationJson.Options)!;
                safeResult.RedactedOrTruncated = sanitized.Changed;
                if (sanitized.Changed && safeResult.Status != EvaluationStatus.NotExecuted) safeResult.Status = EvaluationStatus.PendingReview;
                report.Results[^1] = safeResult;
            }
        report.CallCount = budget.Calls;
        report.AccountedTokens = budget.Tokens;
        report.AccountedCost = budget.Cost;
        report.BudgetStopped = budget.Stopped;
        return report;
    }

    private static string SourceDigest(string root, IEnumerable<string> paths)
    {
        var content = new StringBuilder();
        foreach (var path in paths.Order(StringComparer.Ordinal))
            content.Append(Path.GetRelativePath(root, path).Replace('\\', '/')).Append('\n')
                .Append(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal)).Append('\n');
        return EvaluationJson.Digest(content.ToString());
    }
    private static string? Git(string root, IReadOnlyList<string> args)
    {
        using var process = new Process { StartInfo = new("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        if (!process.Start()) return null;
        var text = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? text.Trim() : null;
    }
}
