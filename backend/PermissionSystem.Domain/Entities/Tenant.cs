using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    public ICollection<Department> Departments { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];

    public ICollection<Role> Roles { get; set; } = [];
}
