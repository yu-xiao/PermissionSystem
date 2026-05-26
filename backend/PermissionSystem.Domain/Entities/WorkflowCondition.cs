using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class WorkflowCondition : BaseEntity
{
    public Guid DefinitionId { get; set; }

    public WorkflowDefinition? Definition { get; set; }

    public string NodeKey { get; set; } = string.Empty;

    public string ConditionName { get; set; } = string.Empty;

    public string ExpressionJson { get; set; } = string.Empty;

    public int Sort { get; set; }

    public ICollection<WorkflowEdge> Edges { get; set; } = [];
}
