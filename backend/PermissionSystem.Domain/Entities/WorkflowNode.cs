using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class WorkflowNode : BaseEntity
{
    public Guid DefinitionId { get; set; }

    public WorkflowDefinition? Definition { get; set; }

    public string NodeKey { get; set; } = string.Empty;

    public string NodeName { get; set; } = string.Empty;

    public WorkflowNodeType NodeType { get; set; }

    public WorkflowApproverType? ApproverType { get; set; }

    public string? ApproverIds { get; set; }

    public WorkflowApprovalMode? ApprovalMode { get; set; }

    public string? ConfigJson { get; set; }

    public decimal PositionX { get; set; }

    public decimal PositionY { get; set; }

    public int Sort { get; set; }
}
