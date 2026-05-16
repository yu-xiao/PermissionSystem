using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class UserRole : BaseEntity
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}
