using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class AiKnowledgeDocumentRole : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid RoleId { get; set; }
}
