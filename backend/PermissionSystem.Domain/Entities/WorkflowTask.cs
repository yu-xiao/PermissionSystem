using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class WorkflowTask : BaseEntity
{
    public Guid InstanceId { get; set; }

    public WorkflowInstance? Instance { get; set; }

    public string NodeKey { get; set; } = string.Empty;

    public string NodeName { get; set; } = string.Empty;

    public Guid ApproverUserId { get; set; }

    public string ApproverUserName { get; set; } = string.Empty;

    public WorkflowTaskStatus Status { get; set; } = WorkflowTaskStatus.Pending;

    public DateTimeOffset AssignedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<WorkflowRecord> Records { get; set; } = [];
}
