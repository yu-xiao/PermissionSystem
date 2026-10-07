using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class AiKnowledgeRunReference : BaseEntity
{
    public Guid RunId { get; set; }
    public string InvocationId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid VersionId { get; set; }
    public Guid ChunkId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
}
