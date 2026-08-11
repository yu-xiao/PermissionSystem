using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class InboxMessage : BaseEntity
{
    public string MessageId { get; set; } = string.Empty;

    public string Consumer { get; set; } = string.Empty;

    public string MessageType { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public string Status { get; set; } = "Processing";

    public string? ErrorMessage { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
