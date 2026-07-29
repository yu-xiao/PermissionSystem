namespace PermissionSystem.Api.Configuration;

public static class StartupSecurityValidator
{
    private static readonly string[] WildcardHosts = ["*", "0.0.0.0", "[::]"];

    public static void ValidateProductionConfiguration(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var allowedHosts = ParseAllowedHosts(configuration["AllowedHosts"]);
        if (allowedHosts.Length == 0 || allowedHosts.Any(IsWildcardHost))
        {
            throw new InvalidOperationException(
                "Production requires explicit AllowedHosts configuration and does not allow wildcard hosts.");
        }

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("Production requires at least one Cors:AllowedOrigins entry.");
        }

        foreach (var origin in allowedOrigins)
        {
            if (!TryValidateOrigin(origin))
            {
                throw new InvalidOperationException(
                    $"CORS origin '{origin}' must be an absolute HTTP or HTTPS origin without path, query, fragment, or wildcard.");
            }
        }
    }

    private static string[] ParseAllowedHosts(string? configuredHosts)
    {
        return string.IsNullOrWhiteSpace(configuredHosts)
            ? []
            : configuredHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsWildcardHost(string host)
    {
        return WildcardHosts.Contains(host, StringComparer.OrdinalIgnoreCase) || host.Contains('*');
    }

    private static bool TryValidateOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || origin.Contains('*'))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        return uri.AbsolutePath == "/" &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment) &&
            string.IsNullOrEmpty(uri.UserInfo);
    }
}
