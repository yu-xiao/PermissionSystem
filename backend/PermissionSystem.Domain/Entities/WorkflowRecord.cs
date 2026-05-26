using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class WorkflowRecord : BaseEntity
{
    public Guid InstanceId { get; set; }

    public WorkflowInstance? Instance { get; set; }

    public Guid? TaskId { get; set; }

    public WorkflowTask? Task { get; set; }

    public string? NodeKey { get; set; }

    public string? NodeName { get; set; }

    public Guid? OperatorUserId { get; set; }

    public string? OperatorUserName { get; set; }

    public WorkflowActionType Action { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset OperatedAt { get; set; }
}
