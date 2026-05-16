using System.Security.Claims;
using OpenIddict.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId => TryGetGuid(ClaimConstants.UserId) ?? TryGetGuid(OpenIddictConstants.Claims.Subject);

    public Guid? TenantId => TryGetGuid(ClaimConstants.TenantId);

    public string? Username => FindFirstValue(ClaimConstants.Username) ?? FindFirstValue(OpenIddictConstants.Claims.Name);

    public IReadOnlyCollection<string> Roles => FindValues(OpenIddictConstants.Claims.Role);

    public IReadOnlyCollection<string> PermissionCodes => FindValues(ClaimConstants.PermissionCode);

    public bool IsSuperAdmin => Roles.Contains(ClaimConstants.SuperAdminRoleCode, StringComparer.OrdinalIgnoreCase);

    public bool HasPermission(string permissionCode)
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(permissionCode))
        {
            return false;
        }

        return IsSuperAdmin || PermissionCodes.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    private Guid? TryGetGuid(string claimType)
    {
        var value = FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private string? FindFirstValue(string claimType)
    {
        return User?.FindFirst(claimType)?.Value;
    }

    private IReadOnlyCollection<string> FindValues(string claimType)
    {
        return User?
            .FindAll(claimType)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }
}
