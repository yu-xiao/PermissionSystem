using Microsoft.AspNetCore.Authorization;

namespace PermissionSystem.Api.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        PermissionCode = permissionCode;
        PermissionCodes = permissionCode
            .Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string PermissionCode { get; }

    public IReadOnlyCollection<string> PermissionCodes { get; }
}
