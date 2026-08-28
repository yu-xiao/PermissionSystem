using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class AiDocumentDraftValidation : BaseEntity
{
    public Guid DraftId { get; set; }

    public int DraftVersion { get; set; }

    public string PayloadHash { get; set; } = string.Empty;

    public bool IsValid { get; set; }

    public string ErrorsJson { get; set; } = "[]";

    public DateTimeOffset ValidatedAt { get; set; }
}
