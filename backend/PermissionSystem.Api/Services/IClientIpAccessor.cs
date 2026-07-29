namespace PermissionSystem.Api.Services;

public interface IClientIpAccessor
{
    string GetClientIp(HttpContext context);
}
