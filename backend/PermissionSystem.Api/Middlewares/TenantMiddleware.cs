using OpenIddict.Abstractions;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Middlewares;

public sealed class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantResolver tenantResolver,
        ITenantContext tenantContext)
    {
        var resolvedTenant = tenantResolver.Resolve(context);
        var isSuperAdmin = IsSuperAdmin(context);
        var claimTenantId = GetClaimTenantId(context);

        tenantContext.MarkAsSuperAdmin(isSuperAdmin);

        if (!isSuperAdmin && claimTenantId.HasValue)
        {
            tenantContext.SetTenant(claimTenantId.Value, "Claims");
        }
        else
        {
            tenantContext.SetTenant(resolvedTenant.TenantId, resolvedTenant.Source);
        }

        if (isSuperAdmin && !string.Equals(resolvedTenant.Source, "Header", StringComparison.OrdinalIgnoreCase))
        {
            tenantContext.DisableTenantFilter();
        }

        await _next(context);
    }

    private static bool IsSuperAdmin(HttpContext context)
    {
        return context.User
            .FindAll(OpenIddictConstants.Claims.Role)
            .Any(claim => string.Equals(claim.Value, ClaimConstants.SuperAdminRoleCode, StringComparison.OrdinalIgnoreCase));
    }

    private static Guid? GetClaimTenantId(HttpContext context)
    {
        var value = context.User.FindFirst(ClaimConstants.TenantId)?.Value;
        return Guid.TryParse(value, out var tenantId) ? tenantId : null;
    }
}
