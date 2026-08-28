using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiDocumentConfirmation : BaseEntity
{
    public Guid DraftId { get; set; }

    public Guid RunId { get; set; }

    public Guid ActorUserId { get; set; }

    public int DraftVersion { get; set; }

    public int ConfirmationVersion { get; set; } = 1;

    public string PayloadHash { get; set; } = string.Empty;

    public string HandlerVersion { get; set; } = string.Empty;

    public AiDocumentConfirmationStatus Status { get; set; } = AiDocumentConfirmationStatus.Confirmed;

    public DateTimeOffset ConfirmedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
