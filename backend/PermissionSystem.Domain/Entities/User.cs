using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class User : BaseEntity
{
    public Guid? DepartmentId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NormalizedUserName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsBuiltin { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public Department? Department { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];

    public UserDataScope? DataScope { get; set; }
}
