using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class Role : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsBuiltin { get; set; }

    public int Sort { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];

    public ICollection<RoleMenu> RoleMenus { get; set; } = [];

    public ICollection<RolePermission> RolePermissions { get; set; } = [];

    public RoleDataScope? DataScope { get; set; }
}
