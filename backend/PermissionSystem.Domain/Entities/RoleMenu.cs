using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class RoleMenu : BaseEntity
{
    public Guid RoleId { get; set; }

    public Guid MenuId { get; set; }

    public Role Role { get; set; } = null!;

    public Menu Menu { get; set; } = null!;
}
