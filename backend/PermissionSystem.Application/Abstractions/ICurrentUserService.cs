namespace PermissionSystem.Application.Abstractions;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    Guid? TenantId { get; }

    Guid? DepartmentId { get; }

    string? SessionId { get; }

    string? Username { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlyCollection<string> PermissionCodes { get; }

    bool IsSuperAdmin { get; }

    bool IsCurrentUserSuperAdmin();

    bool IsCurrentUserAdmin();

    bool CanManageBuiltinResources();

    bool HasPermission(string permissionCode);
}
