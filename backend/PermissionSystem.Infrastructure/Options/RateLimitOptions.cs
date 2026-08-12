namespace PermissionSystem.Infrastructure.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public string Provider { get; init; } = RateLimitProviderNames.Memory;

    public bool Enabled { get; init; } = true;

    public int GlobalPermitLimit { get; init; } = 120;

    public int GlobalWindowSeconds { get; init; } = 60;

    public int LoginPermitLimit { get; init; } = 5;

    public int LoginWindowSeconds { get; init; } = 60;

    public int RefreshTokenPermitLimit { get; init; } = 20;

    public int RefreshTokenWindowSeconds { get; init; } = 60;

    public int ApiKeyWindowSeconds { get; init; } = 60;

    public int WebhookPermitLimit { get; init; } = 30;

    public int WebhookWindowSeconds { get; init; } = 60;

    public int ReportPermitLimit { get; init; } = 30;

    public int ReportWindowSeconds { get; init; } = 60;

    public bool UseRedis()
    {
        return string.Equals(Provider, RateLimitProviderNames.Redis, StringComparison.OrdinalIgnoreCase);
    }
}

public static class RateLimitProviderNames
{
    public const string Memory = "Memory";
    public const string Redis = "Redis";
}
