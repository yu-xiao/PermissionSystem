using OpenIddict.Abstractions;
using Hangfire.Dashboard;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Authorization;

public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var isSuperAdmin = user
            .FindAll(OpenIddictConstants.Claims.Role)
            .Any(claim => string.Equals(claim.Value, ClaimConstants.SuperAdminRoleCode, StringComparison.OrdinalIgnoreCase));
        if (isSuperAdmin)
        {
            return true;
        }

        return user
            .FindAll(ClaimConstants.PermissionCode)
            .Any(claim => string.Equals(claim.Value, "system:job:view", StringComparison.OrdinalIgnoreCase));
    }
}
