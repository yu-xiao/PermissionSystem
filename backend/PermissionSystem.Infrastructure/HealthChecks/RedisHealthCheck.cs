using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace PermissionSystem.Infrastructure.HealthChecks;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ping = await _connectionMultiplexer.GetDatabase().PingAsync();
            return ping >= TimeSpan.Zero
                ? HealthCheckResult.Healthy("Redis is available.")
                : HealthCheckResult.Unhealthy("Redis ping check failed.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable.", exception);
        }
    }
}
