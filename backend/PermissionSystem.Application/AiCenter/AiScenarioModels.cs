using System.Text.Json.Serialization;
using PermissionSystem.Domain.Entities;
using PermissionSystem.AiEvaluations;

namespace PermissionSystem.Application.AiCenter;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AiScenarioConfiguration
{
    public string SupplementPrompt { get; init; } = string.Empty;
    public string[] ToolCodes { get; init; } = AiScenarioCatalog.PermissionTools;
    public int MaxModelRounds { get; init; } = 6;
    public int MaxToolCalls { get; init; } = 10;
    public int MaxHistoryMessages { get; init; } = 20;
    public int MaxRunSeconds { get; init; } = 90;
    public decimal Temperature { get; init; }
    public int MaxTokens { get; init; } = 2048;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveAiScenarioRequest
{
    public string Code { get; init; } = AiScenarioCatalog.PermissionAssistant;
    public string Name { get; init; } = "权限助手";
    public string Description { get; init; } = string.Empty;
    public AiScenarioConfiguration Configuration { get; init; } = new();
    public byte[]? ConcurrencyToken { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AiScenarioChangeRequest
{
    public byte[]? ConcurrencyToken { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool ConfirmInitialBaseline { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportAiScenarioEvaluationRequest
{
    public string ReportJson { get; init; } = string.Empty;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewAiScenarioEvaluationRequest
{
    public string ReportHash { get; init; } = string.Empty;
    public bool GoldenCasesApproved { get; init; }
    public List<CaseReview> Cases { get; init; } = [];
    public string Reason { get; init; } = string.Empty;
    public byte[]? ConcurrencyToken { get; init; }
}

public sealed record AiScenarioResponse(Guid Id, string Code, string Name, string Description,
    bool IsEnabled, Guid? CurrentVersionId, int Revision, byte[] ConcurrencyToken,
    AiScenarioConfiguration Configuration);
public sealed record AiScenarioVersionResponse(Guid Id, int VersionNumber, string ContentHash,
    string BuildIdentity, DateTimeOffset CreatedAt, bool Published, bool Stopped, bool Compatible);
public sealed record AiScenarioEvaluationResponse(Guid Id, Guid VersionId, string ReportHash, string Mode,
    string ModelFingerprint, bool AutomaticPassed, DateTimeOffset CreatedAt, string? ReviewJson);
public sealed record AiScenarioEventResponse(Guid Id, Guid VersionId, Guid? PreviousVersionId,
    Guid? EvaluationId, AiScenarioEventType Type, Guid ActorUserId, string Reason, DateTimeOffset CreatedAt);
public sealed record AiScenarioDetailResponse(AiScenarioResponse Scenario,
    IReadOnlyList<AiScenarioVersionResponse> Versions, IReadOnlyList<AiScenarioEvaluationResponse> Evaluations,
    IReadOnlyList<AiScenarioEventResponse> Events);
public sealed record AiScenarioOption(Guid Id, string Code, string Name, Guid VersionId, int VersionNumber);

public interface IAiBuildIdentity { string Identity { get; } }

public interface IAiScenarioRuntime
{
    Task<AiScenarioVersion> ResolveCurrentAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<AiScenarioSnapshot> ValidateAsync(Guid versionId, CancellationToken cancellationToken = default);
    Task ValidateToolAsync(Guid versionId, string toolCode, CancellationToken cancellationToken = default);
    Task ValidateModelAsync(Guid versionId, AiProviderConfig provider, CancellationToken cancellationToken = default);
}

public interface IAiScenarioService : IAiScenarioRuntime
{
    Task<IReadOnlyList<AiScenarioResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiScenarioOption>> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<AiScenarioDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AiScenarioResponse> SaveAsync(SaveAiScenarioRequest request, CancellationToken cancellationToken = default);
    Task<AiScenarioVersionResponse> FreezeAsync(Guid id, AiScenarioChangeRequest request, CancellationToken cancellationToken = default);
    Task<AiScenarioResponse> CopyToDraftAsync(Guid versionId, AiScenarioChangeRequest request, CancellationToken cancellationToken = default);
    Task<AiScenarioSnapshot> ExportAsync(Guid versionId, CancellationToken cancellationToken = default);
    Task<AiScenarioEvaluationResponse> ImportAsync(Guid versionId, ImportAiScenarioEvaluationRequest request, CancellationToken cancellationToken = default);
    Task<EvaluationReport> GetEvaluationReportAsync(Guid evaluationId, CancellationToken cancellationToken = default);
    Task ReviewAsync(Guid evaluationId, ReviewAiScenarioEvaluationRequest request, CancellationToken cancellationToken = default);
    Task PublishAsync(Guid versionId, AiScenarioChangeRequest request, bool rollback = false, CancellationToken cancellationToken = default);
    Task StopAsync(Guid versionId, AiScenarioChangeRequest request, CancellationToken cancellationToken = default);
    Task RevokeEvaluationAsync(Guid evaluationId, AiScenarioChangeRequest request, CancellationToken cancellationToken = default);
}
