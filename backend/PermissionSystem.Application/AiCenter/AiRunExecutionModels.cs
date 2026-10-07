using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Application.AiCenter;

public interface IAiRunSubmissionService
{
    Task<AiRunResponse> GetSubmissionAsync(Guid conversationId, string submissionKey, CancellationToken cancellationToken = default);
    Task<AiRunResponse> SubmitAsync(Guid conversationId, SendAiMessageRequest request, string submissionKey,
        CancellationToken cancellationToken = default);
    Task<AiRunResponse> SubmitRetryAsync(Guid runId, string submissionKey, CancellationToken cancellationToken = default);
    Task<AiRunResponse> WaitAsync(Guid runId, CancellationToken cancellationToken = default);
}

public interface IAiRunExecutionService
{
    Task ExecuteAsync(Guid runId, Guid leaseId, CancellationToken cancellationToken = default);
}

public interface IAiRunExecutionStore
{
    Task<AiRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(Guid TenantId, Guid RunId)>> ListPendingAsync(int limit, CancellationToken cancellationToken = default);
    Task<AiRun?> ClaimAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<bool> HeartbeatAsync(Guid runId, Guid leaseId, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid runId, CancellationToken cancellationToken = default);
    Task TerminateAsync(Guid runId, Guid leaseId, string errorCode, CancellationToken cancellationToken = default);
}

// Scoped to a single execution; infrastructure enforces this fence for every persistence operation.
public sealed class AiRunExecutionFence
{
    public Guid? RunId { get; private set; }
    public Guid LeaseId { get; private set; }
    public bool IsSettlement { get; set; }
    public void Begin(Guid runId, Guid leaseId) { RunId = runId; LeaseId = leaseId; }
}

public sealed class AiRunLeaseLostException : Exception
{
    public AiRunLeaseLostException() : base("The AI execution lease is no longer valid.") { }
}

public sealed record AiToolProgress(string InvocationId, string ToolCode,
    PermissionSystem.Domain.Enums.AiInvocationStatus Status, DateTimeOffset? CompletedAt);
