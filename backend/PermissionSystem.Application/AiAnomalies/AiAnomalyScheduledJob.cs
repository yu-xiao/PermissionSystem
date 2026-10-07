using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Application.AiAnomalies;

public sealed class AiAnomalyScheduledJob(IAiAnomalyExecutionHost host, IDistributedLock distributedLock)
{
    public async Task ExecuteAsync(Guid taskId, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await distributedLock.ExecuteWithLockAsync($"hangfire:scheduled-task:{taskId:N}",
                token => host.ExecuteTaskAsync(taskId, token), TimeSpan.FromMinutes(1), TimeSpan.Zero, timeout.Token);
        }
        catch (TimeoutException) { await host.RecordSkippedAsync(taskId, ct); }
    }
}
