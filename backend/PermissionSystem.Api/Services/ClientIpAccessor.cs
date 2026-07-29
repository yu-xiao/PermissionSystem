namespace PermissionSystem.Api.Services;

public sealed class ClientIpAccessor : IClientIpAccessor
{
    public string GetClientIp(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return string.Empty;
        }

        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4().ToString()
            : address.ToString();
    }
}
