using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiConversation : BaseEntity
{
    public Guid UserId { get; set; }

    public string AgentCode { get; set; } = string.Empty;

    public string AgentVersion { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public AiConversationStatus Status { get; set; } = AiConversationStatus.Active;

    public DateTimeOffset LastMessageAt { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    public DateTimeOffset RetentionUntil { get; set; }
}
