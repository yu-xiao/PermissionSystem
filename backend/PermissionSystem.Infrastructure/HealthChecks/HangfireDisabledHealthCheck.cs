using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PermissionSystem.Infrastructure.HealthChecks;

public sealed class HangfireDisabledHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(HealthCheckResult.Healthy(
            "Hangfire is disabled.",
            new Dictionary<string, object>
            {
                ["enabled"] = false
            }));
    }
}
