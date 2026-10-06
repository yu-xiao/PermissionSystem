using Microsoft.AspNetCore.Authorization;
using PermissionSystem.Application.Permissions;

namespace PermissionSystem.Api.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        PermissionCode = permissionCode;
        PermissionCodes = PermissionEvaluation.ParseRequirement(permissionCode);
    }

    public string PermissionCode { get; }

    public IReadOnlyCollection<string> PermissionCodes { get; }
}
