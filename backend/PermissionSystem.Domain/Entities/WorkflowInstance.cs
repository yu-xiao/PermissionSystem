using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class WorkflowInstance : BaseEntity
{
    public Guid DefinitionId { get; set; }

    public WorkflowDefinition? Definition { get; set; }

    public string DefinitionCode { get; set; } = string.Empty;

    public string DefinitionName { get; set; } = string.Empty;

    public string BusinessType { get; set; } = string.Empty;

    public string BusinessId { get; set; } = string.Empty;

    public string BusinessTitle { get; set; } = string.Empty;

    public Guid StarterUserId { get; set; }

    public string StarterUserName { get; set; } = string.Empty;

    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Running;

    public string? CurrentNodeKey { get; set; }

    public string? FormDataJson { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<WorkflowTask> Tasks { get; set; } = [];

    public ICollection<WorkflowRecord> Records { get; set; } = [];

    public ICollection<WorkflowCc> Ccs { get; set; } = [];
}
