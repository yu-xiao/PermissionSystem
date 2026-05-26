using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class WorkflowEdge : BaseEntity
{
    public Guid DefinitionId { get; set; }

    public WorkflowDefinition? Definition { get; set; }

    public string FromNodeKey { get; set; } = string.Empty;

    public string ToNodeKey { get; set; } = string.Empty;

    public Guid? ConditionId { get; set; }

    public WorkflowCondition? Condition { get; set; }

    public bool IsDefault { get; set; }

    public int Sort { get; set; }
}
