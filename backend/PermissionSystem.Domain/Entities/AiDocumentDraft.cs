using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiDocumentDraft : BaseEntity
{
    public Guid ConversationId { get; set; }

    public Guid RunId { get; set; }

    public string SourceInvocationId { get; set; } = string.Empty;

    public Guid ActorUserId { get; set; }

    public string BusinessType { get; set; } = string.Empty;

    public string HandlerVersion { get; set; } = string.Empty;

    public AiDocumentDraftStatus Status { get; set; }

    public int DraftVersion { get; set; } = 1;

    public string PayloadJson { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? LastValidatedAt { get; set; }
}
