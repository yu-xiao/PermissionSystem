using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PermissionSystem.Application.AiCenter;

namespace PermissionSystem.AiEvaluations;

public static class EvaluationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Options);


}

public sealed class EvaluationInputException(string message) : Exception(message);
public enum EvaluationMode { Offline, Live }
public enum EvaluationStatus { Passed, Failed, PendingReview, NotApplicable, NotExecuted }

public sealed class EvaluationSuite
{
    public int Version { get; init; } = 1;
    public string FixtureVersion { get; init; } = "1.0";
    public string ReviewStatus { get; init; } = "PendingReview";
    public List<EvaluationCase> Cases { get; init; } = [];

    public void Validate()
    {
        if (Version != 1 || FixtureVersion != "1.0" || Cases.Count is < 1 or > 100 ||
            Cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != Cases.Count)
            throw new EvaluationInputException("Invalid suite version, size or duplicate case ID.");
        foreach (var item in Cases)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(item.Id, "^AIC004-[0-9]{2}$") || item.Version != 1 ||
                string.IsNullOrWhiteSpace(item.Name) || item.Variants.Count is < 1 or > 20 ||
                item.Variants.Select(v => v.Id).Distinct(StringComparer.Ordinal).Count() != item.Variants.Count)
                throw new EvaluationInputException("Invalid case or duplicate variant ID.");
            foreach (var variant in item.Variants)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(variant.Id, "^[a-z0-9-]{1,50}$") || variant.Steps.Count is < 1 or > 4)
                    throw new EvaluationInputException("Invalid variant ID or steps.");
                foreach (var step in variant.Steps)
                {
                    if (string.IsNullOrWhiteSpace(step.Input) || step.Input.Length > 4000 || step.Calls.Count > 10 ||
                        step.Expected.MinCalls < 0 || step.Expected.MaxCalls < step.Expected.MinCalls ||
                        step.Expected.Parameters.ValueKind != JsonValueKind.Object ||
                        step.Calls.Any(call => call.Arguments.ValueKind != JsonValueKind.Object))
                        throw new EvaluationInputException("Invalid step or tool constraints.");
                    if (variant.LiveApplicable && (step.GatewayError is not null || step.MissingUsage))
                        throw new EvaluationInputException("Injected supplier behavior must be offline-only.");
                }
            }
        }
    }
}

public sealed class EvaluationCase
{
    public required string Id { get; init; }
    public int Version { get; init; } = 1;
    public required string Name { get; init; }
    public required string Category { get; init; }
    public bool SafetyCritical { get; init; }
    public List<EvaluationVariant> Variants { get; init; } = [];
}

public sealed class EvaluationVariant
{
    public string Id { get; init; } = "default";
    public bool LiveApplicable { get; init; } = true;
    public FixtureSettings Fixture { get; init; } = new();
    public List<EvaluationStep> Steps { get; init; } = [];
}

public sealed class FixtureSettings
{
    public string? RemovePermission { get; init; }
    public bool Wildcard { get; init; }
    public bool ParentVisible { get; init; } = true;
    public bool ActorEnabled { get; init; } = true;
    public bool TenantActive { get; init; } = true;
    public bool CrossTenantTarget { get; init; }
    public bool TargetDeleted { get; init; }
    public string ActorScope { get; init; } = "All";
}

public enum FixtureMutation { None, RevokeRoleView, ExpireSource }
public sealed class EvaluationStep
{
    public required string Input { get; init; }
    public FixtureMutation Mutation { get; init; }
    public bool SelectFirstResult { get; init; }
    public int? UtcOffsetMinutes { get; init; }
    public List<ScriptedCall> Calls { get; init; } = [];
    public string Answer { get; init; } = "";
    public string? GatewayError { get; init; }
    public bool MissingUsage { get; init; }
    public ExpectedObservation Expected { get; init; } = new();
    public ExpectedObservation? ScenarioExpected { get; init; }
}

public sealed class ScriptedCall
{
    public required string Name { get; init; }
    public JsonElement Arguments { get; init; }
}

public sealed class ExpectedObservation
{
    public string Status { get; init; } = "Completed";
    public string? ErrorCode { get; init; }
    public string? ToolCode { get; init; }
    public int MinCalls { get; init; } = 1;
    public int MaxCalls { get; init; } = 1;
    public JsonElement Parameters { get; init; } = EvaluationJson.Element(new { });
    public string? Conclusion { get; init; }
    public string? EvaluationBasis { get; init; }
    public List<string> EvidenceSources { get; init; } = [];
    public string? CheckCode { get; init; }
    public string? CheckStatus { get; init; }
    public long? TotalCount { get; init; }
    public bool NoEvidence { get; init; }
    public bool Clarification { get; init; }
    public bool UsageEstimated { get; init; }
    public bool EvidenceVisible { get; init; } = true;
    public List<string> RequiredText { get; init; } = [];
    public List<string> ForbiddenText { get; init; } = ["UNREADABLE-CANARY", "正式单据已创建"];
}

public sealed record BudgetLimits(int MaxCalls, long MaxTokens, decimal MaxEstimatedCost);

public sealed record EvaluationCheck(string Code, bool Passed, string Reason, string Dimension = "Fact");
public sealed record ToolObservation(string ToolCode, JsonElement Arguments, string Status, JsonElement? EffectiveParameters,
    AiStructuredResult? ServerResult = null);
public sealed record UsageObservation(string Status, int? InputTokens, int? OutputTokens, int? EstimatedInputTokens,
    int? EstimatedOutputTokens, decimal? EstimatedCost, string? Currency);
public sealed record ProposedCall(string Name, JsonElement Arguments);
public sealed record ToolSnapshot(string Name, string ToolCode, string Version, string DescriptionHash, string InputSchemaHash, string OutputSchemaHash);
public sealed record ModelObservation(string RequestedModel, string? ResponseModel, string PromptHash,
    IReadOnlyList<ToolSnapshot> Tools, decimal? Temperature, int? MaxTokens, int TimeoutSeconds,
    IReadOnlyList<ProposedCall> ProposedCalls, string? Answer, string? ErrorCode,
    int? InputTokens, int? OutputTokens, int ReservedTokens, decimal AccountedCost, bool Estimated, long DurationMilliseconds)
{
    public string? BasePromptHash { get; init; }
}

public sealed class StepObservation
{
    public required string Input { get; init; }
    public string Status { get; set; } = "NotExecuted";
    public string? ErrorCode { get; set; }
    public string Output { get; set; } = "";
    public List<AiStructuredResult> Evidence { get; set; } = [];
    public List<ToolObservation> Tools { get; set; } = [];
    public List<ModelObservation> Models { get; set; } = [];
    public List<UsageObservation> Usage { get; set; } = [];
    public List<EvaluationCheck> Checks { get; set; } = [];
    public string? AgentVersion { get; set; }
    public string? PromptVersion { get; set; }
    public long DurationMilliseconds { get; set; }
}

public sealed class CaseResult
{
    public required string Key { get; init; }
    public required string CaseHash { get; init; }
    public bool SafetyCritical { get; init; }
    public bool LiveApplicable { get; init; }
    public EvaluationStatus Status { get; set; }
    public List<StepObservation> Steps { get; set; } = [];
    public bool RedactedOrTruncated { get; set; }
    public string? Reason { get; set; }
}

public sealed class EvaluationReport
{
    public int Version { get; init; } = 1;
    public string CheckerVersion { get; init; } = "1.0";
    public required string SuiteHash { get; init; }
    public required string FixtureHash { get; init; }
    public required string CheckerHash { get; init; }
    public required string SourceHash { get; init; }
    public string? SnapshotHash { get; init; }
    public string? BuildIdentity { get; init; }
    public string? ModelFingerprint { get; init; }
    public string? GitCommit { get; init; }
    public bool WorkingTreeDirty { get; init; }
    public EvaluationMode Mode { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public string ProviderAlias { get; init; } = "offline";
    public string Model { get; init; } = "scripted";
    public string Runtime { get; init; } = "";
    public string? ComplianceReference { get; init; }
    public decimal? Temperature { get; init; }
    public int MaxTokens { get; init; }
    public int TimeoutSeconds { get; init; }
    public string? Currency { get; init; }
    public decimal? InputPricePerMillion { get; init; }
    public decimal? OutputPricePerMillion { get; init; }
    public BudgetLimits? Limits { get; init; }
    public int CallCount { get; set; }
    public long AccountedTokens { get; set; }
    public decimal AccountedCost { get; set; }
    public bool BudgetStopped { get; set; }
    public List<CaseManifest> Manifest { get; init; } = [];
    public List<CaseResult> Results { get; init; } = [];
    public bool AutomaticChecksPassed => EvaluationGate.HasCompleteCoverage(this) && !BudgetStopped && Results.All(r =>
        r.Status == EvaluationStatus.NotApplicable || r.Status is EvaluationStatus.Passed or EvaluationStatus.PendingReview &&
        r.Steps.Count > 0 && r.Steps.All(s => s.Checks.Count > 0 && s.Checks.All(c => c.Passed)) && !r.RedactedOrTruncated);
    public EvaluationScores Scores => EvaluationGate.Scores(this);
}
public sealed record CaseManifest(string Key, string CaseHash, int StepCount, bool SafetyCritical, bool LiveApplicable);
public sealed record EvaluationScores(int ExpectedVariants, int ApplicableVariants, int CompleteVariants,
    decimal ParameterCorrectness, decimal FactCorrectness, int SafetyFailures);

public sealed class ReviewDocument
{
    public int Version { get; init; } = 1;
    public required string Reviewer { get; init; }
    public required string SuiteHash { get; init; }
    public required string ReportHash { get; init; }
    public DateTimeOffset ReviewedAt { get; init; }
    public bool GoldenCasesApproved { get; init; }
    public List<CaseReview> Cases { get; init; } = [];
}
public sealed record CaseReview(string Key, string CaseHash, bool Passed, string Notes);
public sealed record GateResult(bool Passed, IReadOnlyList<string> Reasons);
public sealed record ComparisonResult(bool Comparable, bool NoRegression, IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Regressions, long DurationChangeMilliseconds, decimal CostChange, GateResult CandidateGate,
    EvaluationScores BaselineScores, EvaluationScores CandidateScores, IReadOnlyList<string> VersionChanges);
