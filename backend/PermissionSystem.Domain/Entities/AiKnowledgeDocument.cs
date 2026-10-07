using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class AiKnowledgeDocument : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public string Classification { get; set; } = "Synthetic";
    public Guid? CurrentVersionId { get; set; }
    public int AccessVersion { get; set; } = 1;
    public int LastVersionNumber { get; set; }
}
