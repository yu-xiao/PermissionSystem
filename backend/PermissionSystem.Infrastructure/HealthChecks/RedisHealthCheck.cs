using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PermissionSystem.Infrastructure.HealthChecks;

public sealed class RedisHealthCheck : IHealthCheck
{
    private const string HealthCheckKey = "health:redis";

    private readonly IDistributedCache _distributedCache;

    public RedisHealthCheck(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.SetStringAsync(
                HealthCheckKey,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                },
                cancellationToken);

            return HealthCheckResult.Healthy("Redis is available.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable.", exception);
        }
    }
}
