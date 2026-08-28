using System.Net;
using System.Net.Sockets;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Infrastructure.Ai;

internal static class OpenAiCompatibleEndpointValidator
{
    public static Uri ValidateConfiguration(OpenAiCompatibleOptions options)
    {
        if (!options.Enabled)
        {
            throw new AiModelGatewayException(
                "provider_disabled",
                ErrorCode.Conflict,
                "The AI model provider is disabled.",
                false);
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw InvalidConfiguration("The AI provider BaseUrl is invalid.");
        }

        if (baseUri.Scheme == Uri.UriSchemeHttp && !options.AllowInsecureHttp)
        {
            throw InvalidConfiguration("The AI provider must use HTTPS.");
        }

        if (options.AllowedHosts.Length == 0 ||
            !options.AllowedHosts.Contains(baseUri.IdnHost, StringComparer.OrdinalIgnoreCase))
        {
            throw InvalidConfiguration("The AI provider host is not allowlisted.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.Model))
        {
            throw InvalidConfiguration("The AI provider credentials and model must be configured.");
        }

        if (options.TimeoutSeconds is < 1 or > 120)
        {
            throw InvalidConfiguration("The AI provider timeout must be between 1 and 120 seconds.");
        }

        if (string.IsNullOrWhiteSpace(options.ChatCompletionsPath) ||
            Uri.TryCreate(options.ChatCompletionsPath, UriKind.Absolute, out _))
        {
            throw InvalidConfiguration("The AI provider chat completions path is invalid.");
        }

        var normalizedBaseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? options.BaseUrl
            : options.BaseUrl + "/";
        return new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), options.ChatCompletionsPath.TrimStart('/'));
    }

    public static async Task ValidateResolvedAddressesAsync(
        Uri endpoint,
        bool allowPrivateNetwork,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(endpoint.DnsSafeHost, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new AiModelGatewayException(
                "provider_unreachable",
                ErrorCode.InternalServerError,
                "The AI provider host could not be resolved.",
                true,
                innerException: exception);
        }

        if (addresses.Length == 0 || (!allowPrivateNetwork && addresses.Any(IsPrivateOrReserved)))
        {
            throw new AiModelGatewayException(
                "provider_endpoint_blocked",
                ErrorCode.Forbidden,
                "The AI provider endpoint is blocked by the outbound network policy.",
                false);
        }
    }

    private static bool IsPrivateOrReserved(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6None) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6Multicast ||
            address.IsIPv6SiteLocal)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return true;
            }

            return address.IsIPv4MappedToIPv6 && IsPrivateOrReserved(address.MapToIPv4());
        }

        var octets = address.GetAddressBytes();
        return octets[0] == 0 ||
            octets[0] == 10 ||
            octets[0] == 127 ||
            (octets[0] == 100 && octets[1] is >= 64 and <= 127) ||
            (octets[0] == 169 && octets[1] == 254) ||
            (octets[0] == 172 && octets[1] is >= 16 and <= 31) ||
            (octets[0] == 192 && octets[1] == 168) ||
            (octets[0] == 198 && octets[1] is 18 or 19) ||
            octets[0] >= 224;
    }

    private static AiModelGatewayException InvalidConfiguration(string message)
    {
        return new AiModelGatewayException(
            "provider_configuration_invalid",
            ErrorCode.ValidationFailed,
            message,
            false);
    }
}
