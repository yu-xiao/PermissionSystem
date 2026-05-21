using OpenIddict.Abstractions;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Services;

public sealed class TenantResolver : ITenantResolver
{
    public const string TenantHeaderName = "X-Tenant-Id";
    private static readonly Guid FallbackDefaultTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private readonly IConfiguration _configuration;

    public TenantResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TenantResolveResult Resolve(HttpContext context)
    {
        if (TryGetHeaderTenantId(context, out var headerTenantId))
        {
            return new TenantResolveResult(headerTenantId, "Header");
        }

        if (TryGetClaimTenantId(context, out var claimTenantId))
        {
            return new TenantResolveResult(claimTenantId, "Claims");
        }

        return new TenantResolveResult(GetDefaultTenantId(), "Default");
    }

    private static bool TryGetHeaderTenantId(HttpContext context, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var value = context.Request.Headers[TenantHeaderName].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out tenantId);
    }

    private static bool TryGetClaimTenantId(HttpContext context, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var value = context.User.FindFirst(ClaimConstants.TenantId)?.Value;
        return !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out tenantId);
    }

    private Guid GetDefaultTenantId()
    {
        var configuredValue = _configuration["Tenant:DefaultTenantId"];
        return Guid.TryParse(configuredValue, out var tenantId)
            ? tenantId
            : FallbackDefaultTenantId;
    }
}
