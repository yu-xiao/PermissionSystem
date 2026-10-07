using System.Security.Claims;
using OpenIddict.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AiRunExecutionIdentity? _backgroundIdentity;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AiRunExecutionIdentity? backgroundIdentity = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _backgroundIdentity = backgroundIdentity;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    private AiRunExecutionIdentity? Background => _httpContextAccessor.HttpContext is null ? _backgroundIdentity : null;
    public bool IsAuthenticated => Background?.IsAuthenticated ?? (User?.Identity?.IsAuthenticated == true);

    public Guid? UserId => Background?.UserId ?? TryGetGuid(ClaimConstants.UserId) ?? TryGetGuid(OpenIddictConstants.Claims.Subject);

    public Guid? TenantId => Background?.TenantId ?? TryGetGuid(ClaimConstants.TenantId);

    public Guid? DepartmentId => Background?.DepartmentId ?? TryGetGuid(ClaimConstants.DepartmentId);

    public string? SessionId => Background?.SessionId ?? FindFirstValue(ClaimConstants.SessionId);
    public Guid? SecurityStamp => Background?.SecurityStamp ?? TryGetGuid(ClaimConstants.SecurityStamp);

    public string? Username =>
        Background?.Username ??
        FindFirstValue(ClaimConstants.Username) ??
        FindFirstValue(ClaimConstants.LegacyUsername) ??
        FindFirstValue(OpenIddictConstants.Claims.Name);

    public IReadOnlyCollection<string> Roles => Background?.Roles ?? FindValues(OpenIddictConstants.Claims.Role);

    public IReadOnlyCollection<string> PermissionCodes => Background?.PermissionCodes ?? FindValues(ClaimConstants.PermissionCode);

    public bool IsSuperAdmin => PermissionEvaluation.IsSuperAdmin(Roles);

    public bool IsCurrentUserSuperAdmin()
    {
        return IsSuperAdmin;
    }

    public bool IsCurrentUserAdmin()
    {
        return IsSuperAdmin;
    }

    public bool CanManageBuiltinResources()
    {
        return IsSuperAdmin;
    }

    public bool HasPermission(string permissionCode)
    {
        return PermissionEvaluation.HasPermission(IsAuthenticated, IsSuperAdmin, PermissionCodes, permissionCode);
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
