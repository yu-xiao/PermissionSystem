using OpenIddict.Abstractions;
using Hangfire.Dashboard;
using PermissionSystem.Shared.Constants;
using System.Security.Claims;

namespace PermissionSystem.Api.Authorization;

public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public const string ViewPermission = "system:job:view";

    public const string TriggerPermission = "system:job:trigger";

    public bool Authorize(DashboardContext context)
    {
        return Authorize(context.GetHttpContext());
    }

    public static bool Authorize(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

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

        if (!HasPermission(user, ViewPermission))
        {
            return false;
        }

        return IsReadOnlyRequest(httpContext.Request.Method) ||
            HasPermission(user, TriggerPermission);
    }

    private static bool HasPermission(ClaimsPrincipal user, string permissionCode)
    {
        return user
            .FindAll(ClaimConstants.PermissionCode)
            .Any(claim => string.Equals(claim.Value, permissionCode, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReadOnlyRequest(string method)
    {
        return HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
    }
}
