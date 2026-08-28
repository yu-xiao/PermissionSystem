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

        if (configuration.GetValue("RateLimit:Enabled", true) &&
            !string.Equals(configuration["RateLimit:Provider"], "Redis", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Production requires RateLimit:Provider to be Redis when rate limiting is enabled.");
        }

        var cacheProvider = configuration["Cache:Provider"];
        var redisEnabled = configuration.GetValue("Cache:EnableRedis", false);
        if (!redisEnabled || !string.Equals(cacheProvider, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Production requires Cache:Provider to be Redis for distributed idempotency.");
        }

        var issuerValue = configuration["OpenIddict:Issuer"];
        if (!Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer) ||
            issuer.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(issuer.UserInfo) ||
            !string.IsNullOrEmpty(issuer.Query) ||
            !string.IsNullOrEmpty(issuer.Fragment))
        {
            throw new InvalidOperationException("Production requires an absolute HTTPS OpenIddict:Issuer URL.");
        }

        ValidateAiConfiguration(configuration);
    }

    private static void ValidateAiConfiguration(IConfiguration configuration)
    {
        if (!configuration.GetValue("Ai:Enabled", false))
        {
            return;
        }

        var allowedTenantIds = configuration.GetSection("Ai:AllowedTenantIds").Get<Guid[]>() ?? [];
        if (allowedTenantIds.Length == 0 || allowedTenantIds.Any(id => id == Guid.Empty))
        {
            throw new InvalidOperationException(
                "Production requires at least one explicit Ai:AllowedTenantIds entry when AI is enabled.");
        }

        var encryptionKey = configuration["Security:SystemConfigEncryptionKey"];
        if (string.IsNullOrWhiteSpace(encryptionKey) || encryptionKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Production requires Security:SystemConfigEncryptionKey with at least 32 characters when AI is enabled.");
        }

        var conversationRetentionDays = configuration.GetValue("Ai:ConversationRetentionDays", 30);
        var auditRetentionDays = configuration.GetValue("Ai:AuditRetentionDays", 180);
        if (conversationRetentionDays is < 1 or > 365 ||
            auditRetentionDays is < 30 or > 3650 ||
            auditRetentionDays < conversationRetentionDays)
        {
            throw new InvalidOperationException("AI retention configuration is invalid.");
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
