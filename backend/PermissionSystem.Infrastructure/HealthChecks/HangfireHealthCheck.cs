using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PermissionSystem.Application.Abstractions;

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
            var queueLength = queues.Sum(queue => (long)queue.Length);
            ObservabilityMetrics.RecordHangfireState(queueLength, servers.Count);

            return Task.FromResult(HealthCheckResult.Healthy(
                "Hangfire storage is available.",
                new Dictionary<string, object>
                {
                    ["storage"] = _jobStorage.GetType().Name,
                    ["queueCount"] = queues.Count,
                    ["queueLength"] = queueLength,
                    ["serverCount"] = servers.Count
                }));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Hangfire storage is unavailable.", exception));
        }
    }
}
