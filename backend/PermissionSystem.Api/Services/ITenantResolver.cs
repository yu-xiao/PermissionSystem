namespace PermissionSystem.Api.Services;

public sealed record TenantResolveResult(Guid TenantId, string Source);

public interface ITenantResolver
{
    TenantResolveResult Resolve(HttpContext context);
}
