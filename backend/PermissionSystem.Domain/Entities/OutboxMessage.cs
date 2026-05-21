using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class OutboxMessage : BaseEntity
{
    public string MessageId { get; set; } = string.Empty;

    public string Exchange { get; set; } = string.Empty;

    public string RoutingKey { get; set; } = string.Empty;

    public string MessageType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string? Headers { get; set; }

    public string Status { get; set; } = "Pending";

    public int RetryCount { get; set; }

    public DateTimeOffset? NextRetryAt { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
