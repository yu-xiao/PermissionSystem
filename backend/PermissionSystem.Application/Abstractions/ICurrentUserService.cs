namespace PermissionSystem.Application.Abstractions;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    Guid? TenantId { get; }

    string? Username { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlyCollection<string> PermissionCodes { get; }

    bool IsSuperAdmin { get; }

    bool HasPermission(string permissionCode);
}
