using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PermissionSystem.Infrastructure.HealthChecks;

public sealed class HangfireHealthCheck : IHealthCheck
{
    private readonly JobStorage _jobStorage;

    public HangfireHealthCheck(JobStorage jobStorage)
    {
        _jobStorage = jobStorage;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var monitoringApi = _jobStorage.GetMonitoringApi();
            var queues = monitoringApi.Queues();
            var servers = monitoringApi.Servers();

            return Task.FromResult(HealthCheckResult.Healthy(
                "Hangfire storage is available.",
                new Dictionary<string, object>
                {
                    ["storage"] = _jobStorage.GetType().Name,
                    ["queueCount"] = queues.Count,
                    ["serverCount"] = servers.Count
                }));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Hangfire storage is unavailable.", exception));
        }
    }
}
