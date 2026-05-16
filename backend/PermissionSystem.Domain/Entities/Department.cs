using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class Department : BaseEntity
{
    public Guid? ParentId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Sort { get; set; }

    public bool IsEnabled { get; set; } = true;

    public Department? Parent { get; set; }

    public ICollection<Department> Children { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];
}
