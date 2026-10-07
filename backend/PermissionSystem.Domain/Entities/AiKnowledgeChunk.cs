using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class AiKnowledgeChunk : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid VersionId { get; set; }
    public int Sequence { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}
