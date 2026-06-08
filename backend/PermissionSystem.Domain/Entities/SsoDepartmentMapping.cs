using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class SsoDepartmentMapping : BaseEntity
{
    public Guid ProviderId { get; set; }

    public string ExternalDepartment { get; set; } = string.Empty;

    public Guid LocalDepartmentId { get; set; }

    public SsoProvider? Provider { get; set; }

    public Department? LocalDepartment { get; set; }
}
