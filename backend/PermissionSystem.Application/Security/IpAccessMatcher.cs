using System.Net;

namespace PermissionSystem.Application.Security;

public static class IpAccessMatcher
{
    public static bool AnyMatches(string? patterns, string? ipAddress)
    {
        var ip = NormalizeIp(ipAddress);
        if (ip is null || string.IsNullOrWhiteSpace(patterns))
        {
            return false;
        }

        return patterns
            .Split([',', ';', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(pattern => Matches(pattern, ip));
    }

    public static bool Matches(string? pattern, string? ipAddress)
    {
        var normalizedPattern = NormalizePattern(pattern);
        var normalizedIp = NormalizeIp(ipAddress);
        if (normalizedPattern is null || normalizedIp is null)
        {
            return false;
        }

        if (normalizedPattern == "*")
        {
            return true;
        }

        if (TryMatchCidr(normalizedPattern, normalizedIp, out var cidrMatched))
        {
            return cidrMatched;
        }

        if (normalizedPattern.EndsWith("*", StringComparison.Ordinal))
        {
            return normalizedIp.StartsWith(normalizedPattern[..^1], StringComparison.OrdinalIgnoreCase);
        }

        if (IPAddress.TryParse(normalizedPattern, out var patternAddress) &&
            IPAddress.TryParse(normalizedIp, out var requestAddress))
        {
            return NormalizeAddress(patternAddress).Equals(NormalizeAddress(requestAddress));
        }

        return string.Equals(normalizedPattern, normalizedIp, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMatchCidr(string pattern, string ipAddress, out bool matched)
    {
        matched = false;
        var slashIndex = pattern.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex <= 0 || slashIndex == pattern.Length - 1)
        {
            return false;
        }

        if (!IPAddress.TryParse(pattern[..slashIndex], out var networkAddress) ||
            !IPAddress.TryParse(ipAddress, out var requestAddress) ||
            !int.TryParse(pattern[(slashIndex + 1)..], out var prefixLength))
        {
            return false;
        }

        networkAddress = NormalizeAddress(networkAddress);
        requestAddress = NormalizeAddress(requestAddress);
        var networkBytes = networkAddress.GetAddressBytes();
        var requestBytes = requestAddress.GetAddressBytes();
        if (networkBytes.Length != requestBytes.Length || prefixLength < 0 || prefixLength > networkBytes.Length * 8)
        {
            return true;
        }

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        for (var index = 0; index < fullBytes; index++)
        {
            if (networkBytes[index] != requestBytes[index])
            {
                matched = false;
                return true;
            }
        }

        if (remainingBits == 0)
        {
            matched = true;
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        matched = (networkBytes[fullBytes] & mask) == (requestBytes[fullBytes] & mask);
        return true;
    }

    private static string? NormalizePattern(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return IPAddress.TryParse(trimmed, out var address)
            ? NormalizeAddress(address).ToString()
            : trimmed;
    }

    private static IPAddress NormalizeAddress(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }
}
