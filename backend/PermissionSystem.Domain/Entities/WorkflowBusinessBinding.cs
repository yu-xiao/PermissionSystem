using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class WorkflowBusinessBinding : BaseEntity
{
    public string BusinessType { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public Guid DefinitionId { get; set; }

    public WorkflowDefinition? Definition { get; set; }

    public string DefinitionCode { get; set; } = string.Empty;

    public string DefinitionName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public string? Remark { get; set; }
}
