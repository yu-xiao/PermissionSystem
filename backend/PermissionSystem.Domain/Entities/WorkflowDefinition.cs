using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class WorkflowDefinition : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Version { get; set; } = 1;

    public WorkflowDefinitionStatus Status { get; set; } = WorkflowDefinitionStatus.Draft;

    public bool IsPublished { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public ICollection<WorkflowNode> Nodes { get; set; } = [];

    public ICollection<WorkflowEdge> Edges { get; set; } = [];

    public ICollection<WorkflowCondition> Conditions { get; set; } = [];

    public ICollection<WorkflowInstance> Instances { get; set; } = [];

    public ICollection<WorkflowBusinessBinding> BusinessBindings { get; set; } = [];
}
