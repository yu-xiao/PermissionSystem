using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class RoleDataScope : BaseEntity
{
    public Guid RoleId { get; set; }

    public DataScopeType ScopeType { get; set; } = DataScopeType.All;

    public string? CustomDepartmentIds { get; set; }

    public Role Role { get; set; } = null!;
}
