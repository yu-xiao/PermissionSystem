namespace PermissionSystem.Infrastructure.Options;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public string Provider { get; init; } = CacheProviderNames.Memory;

    public bool EnableRedis { get; init; }

    public int DefaultExpirationMinutes { get; init; } = 30;

    public string KeyPrefix { get; init; } = "PermissionSystem:";

    public bool UseRedis()
    {
        return EnableRedis &&
            string.Equals(Provider, CacheProviderNames.Redis, StringComparison.OrdinalIgnoreCase);
    }
}

public static class CacheProviderNames
{
    public const string Memory = "Memory";

    public const string Redis = "Redis";
}
