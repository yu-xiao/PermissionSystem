using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Resource { get; set; }

    public string? Action { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
