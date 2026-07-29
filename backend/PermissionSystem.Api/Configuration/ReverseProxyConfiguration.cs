using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using PermissionSystem.Api.Options;

namespace PermissionSystem.Api.Configuration;

public static class ReverseProxyConfiguration
{
    public static ReverseProxyOptions AddConfiguredForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(ReverseProxyOptions.SectionName)
            .Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();

        Validate(settings);
        services.Configure<ReverseProxyOptions>(configuration.GetSection(ReverseProxyOptions.SectionName));

        if (!settings.Enabled)
        {
            return settings;
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto |
                ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = settings.ForwardLimit;
            options.RequireHeaderSymmetry = true;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in settings.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in settings.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }

            foreach (var host in ParseAllowedHosts(configuration["AllowedHosts"]))
            {
                options.AllowedHosts.Add(host);
            }
        });

        return settings;
    }

    public static void Validate(ReverseProxyOptions settings)
    {
        if (!settings.Enabled)
        {
            return;
        }

        if (settings.ForwardLimit <= 0)
        {
            throw new InvalidOperationException("ReverseProxy:ForwardLimit must be greater than zero when reverse proxy support is enabled.");
        }

        if (settings.KnownProxies.Length == 0 && settings.KnownNetworks.Length == 0)
        {
            throw new InvalidOperationException("At least one ReverseProxy:KnownProxies or ReverseProxy:KnownNetworks entry is required when reverse proxy support is enabled.");
        }

        foreach (var proxy in settings.KnownProxies)
        {
            if (!IPAddress.TryParse(proxy, out var address) ||
                address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.IPv6Any) ||
                address.Equals(IPAddress.None) ||
                address.Equals(IPAddress.IPv6None))
            {
                throw new InvalidOperationException($"ReverseProxy known proxy '{proxy}' is not a valid unicast IP address.");
            }
        }

        foreach (var network in settings.KnownNetworks)
        {
            if (!System.Net.IPNetwork.TryParse(network, out var parsedNetwork) || parsedNetwork.PrefixLength == 0)
            {
                throw new InvalidOperationException(
                    $"ReverseProxy known network '{network}' is not a valid restricted CIDR network.");
            }
        }
    }

    private static IEnumerable<string> ParseAllowedHosts(string? configuredHosts)
    {
        return string.IsNullOrWhiteSpace(configuredHosts)
            ? []
            : configuredHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
