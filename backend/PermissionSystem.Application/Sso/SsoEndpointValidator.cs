using System.Net;
using System.Net.Sockets;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Sso;

public static class SsoEndpointValidator
{
    public static Uri ValidateConfigured(
        string endpoint,
        ISsoConfiguration configuration,
        string endpointName = "SSO endpoint")
    {
        var uri = Parse(endpoint, configuration, endpointName);
        if (IsAllowedHost(uri, configuration))
        {
            return uri;
        }

        if (IPAddress.TryParse(uri.DnsSafeHost, out var ipAddress) && IsBlockedAddress(ipAddress))
        {
            throw CreateBlockedAddressException(endpointName);
        }

        return uri;
    }

    public static async Task<Uri> ValidateAsync(
        string endpoint,
        ISsoConfiguration configuration,
        string endpointName,
        CancellationToken cancellationToken = default)
    {
        var uri = ValidateConfigured(endpoint, configuration, endpointName);
        if (IsAllowedHost(uri, configuration) || IPAddress.TryParse(uri.DnsSafeHost, out _))
        {
            return uri;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        }
        catch (SocketException)
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                $"{endpointName} host could not be resolved.");
        }

        if (addresses.Length == 0 || addresses.Any(IsBlockedAddress))
        {
            throw CreateBlockedAddressException(endpointName);
        }

        return uri;
    }

    private static Uri Parse(
        string endpoint,
        ISsoConfiguration configuration,
        string endpointName)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(uri.DnsSafeHost) ||
            !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                $"{endpointName} must be an absolute HTTP or HTTPS URI without user information.");
        }

        if (configuration.RequireHttpsMetadata && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "HTTPS metadata is required.");
        }

        return uri;
    }

    private static bool IsAllowedHost(Uri uri, ISsoConfiguration configuration)
    {
        var host = NormalizeHost(uri.DnsSafeHost);
        return configuration.AllowedMetadataHosts.Any(
            allowedHost => string.Equals(NormalizeHost(allowedHost), host, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeHost(string value)
    {
        var host = value.Trim().TrimEnd('.');
        return host.Length > 1 && host[0] == '[' && host[^1] == ']'
            ? host[1..^1]
            : host;
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 0 ||
                bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 198 && bytes[1] is 18 or 19) ||
                (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
                (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) ||
                bytes[0] >= 224;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xFE) == 0xFC ||
                (bytes[0] == 0xFE && (bytes[1] & 0xC0) is 0x80 or 0xC0) ||
                bytes[0] == 0xFF ||
                (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8);
        }

        return true;
    }

    private static BusinessException CreateBlockedAddressException(string endpointName)
    {
        return new BusinessException(
            ErrorCode.ValidationFailed,
            $"{endpointName} resolves to a loopback, private, link-local, or otherwise blocked address.");
    }
}
