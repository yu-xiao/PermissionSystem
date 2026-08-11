using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class DeadLetterMessage : BaseEntity
{
    public string MessageId { get; set; } = string.Empty;

    public string Consumer { get; set; } = string.Empty;

    public string SourceQueue { get; set; } = string.Empty;

    public string Exchange { get; set; } = string.Empty;

    public string RoutingKey { get; set; } = string.Empty;

    public string MessageType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string? Headers { get; set; }

    public int RetryCount { get; set; }

    public string FailureReason { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int ReplayCount { get; set; }

    public DateTimeOffset? LastReplayedAt { get; set; }

    public string? DispositionRemark { get; set; }
}
