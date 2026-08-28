using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiMessage : BaseEntity
{
    public Guid ConversationId { get; set; }

    public AiMessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public AiContentClassification ContentClassification { get; set; } = AiContentClassification.Internal;

    public string ContentDigest { get; set; } = string.Empty;

    public int? TokenCount { get; set; }

    public int Sequence { get; set; }

    public bool ModelGenerated { get; set; }
}
