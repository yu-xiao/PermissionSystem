using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class WorkflowCc : BaseEntity
{
    public Guid InstanceId { get; set; }

    public WorkflowInstance? Instance { get; set; }

    public string NodeKey { get; set; } = string.Empty;

    public Guid CcUserId { get; set; }

    public string CcUserName { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
