using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.Permissions;

public static class PermissionEvaluation
{
    public static IReadOnlyCollection<string> ParseRequirement(string requirement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requirement);
        return requirement
            .Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsSuperAdmin(IEnumerable<string> roles) =>
        roles.Contains(ClaimConstants.SuperAdminRoleCode, StringComparer.OrdinalIgnoreCase);

    public static bool HasPermission(
        bool isAuthenticated,
        bool isSuperAdmin,
        IEnumerable<string> permissions,
        string permissionCode) =>
        isAuthenticated && !string.IsNullOrWhiteSpace(permissionCode) &&
        (isSuperAdmin || permissions.Contains("*", StringComparer.OrdinalIgnoreCase) ||
         permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase));
}

public sealed record PermissionSubject(Guid? TenantId, Guid? UserId, Guid? DepartmentId, bool IsSuperAdmin);
