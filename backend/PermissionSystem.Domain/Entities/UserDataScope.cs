using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class UserDataScope : BaseEntity
{
    public Guid UserId { get; set; }

    public DataScopeType ScopeType { get; set; } = DataScopeType.All;

    public string? CustomDepartmentIds { get; set; }

    public User User { get; set; } = null!;
}
