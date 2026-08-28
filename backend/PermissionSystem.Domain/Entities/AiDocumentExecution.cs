using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiDocumentExecution : BaseEntity
{
    public Guid ConfirmationId { get; set; }

    public int ConfirmationVersion { get; set; }

    public Guid DraftId { get; set; }

    public Guid RunId { get; set; }

    public Guid ActorUserId { get; set; }

    public string BusinessType { get; set; } = string.Empty;

    public string BusinessIdempotencyKey { get; set; } = string.Empty;

    public AiDocumentExecutionStatus Status { get; set; } = AiDocumentExecutionStatus.Executing;

    public Guid? BusinessEntityId { get; set; }

    public string? BusinessNo { get; set; }

    public string? BusinessStatus { get; set; }

    public string TraceId { get; set; } = string.Empty;

    public string? OutboxMessageId { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorSummary { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
