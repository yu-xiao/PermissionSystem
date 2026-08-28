using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.AiActions;

public static class AiDocumentExecutionMessageNames
{
    public const string Exchange = "permission-system.exchange";
    public const string RoutingKey = "ai.document.executed";
}

public sealed class CreateAiDocumentConfirmationRequest
{
    public byte[]? DraftConcurrencyToken { get; init; }
}

public sealed class ExecuteAiDocumentDraftRequest
{
    public Guid ConfirmationId { get; init; }

    public int ConfirmationVersion { get; init; }

    public byte[]? ConfirmationConcurrencyToken { get; init; }

    public byte[]? DraftConcurrencyToken { get; init; }
}

public sealed class AiDocumentConfirmationResponse
{
    public Guid Id { get; init; }

    public Guid DraftId { get; init; }

    public int DraftVersion { get; init; }

    public int ConfirmationVersion { get; init; }

    public string PayloadHash { get; init; } = string.Empty;

    public string HandlerVersion { get; init; } = string.Empty;

    public DateTimeOffset ConfirmedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class AiDocumentExecutionResponse
{
    public Guid ExecutionId { get; init; }

    public Guid DraftId { get; init; }

    public Guid RunId { get; init; }

    public Guid BusinessEntityId { get; init; }

    public string BusinessNo { get; init; } = string.Empty;

    public string BusinessStatus { get; init; } = string.Empty;

    public string LinkUrl { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;

    public DateTimeOffset CompletedAt { get; init; }

    public AiDocumentDraftStatus DraftStatus { get; init; }

    public byte[] DraftConcurrencyToken { get; init; } = [];
}

public sealed class AiDocumentExecutedEvent
{
    public Guid ExecutionId { get; init; }

    public Guid DraftId { get; init; }

    public Guid RunId { get; init; }

    public Guid ActorUserId { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public Guid BusinessEntityId { get; init; }

    public string BusinessNo { get; init; } = string.Empty;

    public string BusinessStatus { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;

    public DateTimeOffset OccurredAt { get; init; }
}

public sealed class AiDocumentExecutionFailureRecord
{
    public Guid TenantId { get; init; }

    public Guid ConfirmationId { get; init; }

    public int ConfirmationVersion { get; init; }

    public Guid DraftId { get; init; }

    public Guid ActorUserId { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string BusinessIdempotencyKey { get; init; } = string.Empty;

    public AiDocumentExecutionStatus Status { get; init; }

    public string TraceId { get; init; } = string.Empty;

    public string ErrorCode { get; init; } = string.Empty;

    public string ErrorSummary { get; init; } = string.Empty;

    public DateTimeOffset OccurredAt { get; init; }
}

public interface IAiDocumentExecutionService
{
    Task<AiDocumentConfirmationResponse> ConfirmAsync(
        Guid draftId,
        CreateAiDocumentConfirmationRequest request,
        CancellationToken cancellationToken = default);

    Task<AiDocumentExecutionResponse> ExecuteAsync(
        Guid draftId,
        ExecuteAiDocumentDraftRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAiDocumentExecutionRecoveryStore
{
    Task<AiDocumentExecution?> GetByBusinessIdempotencyKeyAsync(
        Guid tenantId,
        string businessIdempotencyKey,
        CancellationToken cancellationToken = default);

    Task RecordFailureAsync(
        AiDocumentExecutionFailureRecord record,
        CancellationToken cancellationToken = default);
}
