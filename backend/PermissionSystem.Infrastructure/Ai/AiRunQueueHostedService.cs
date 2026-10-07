using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Tenants;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiRunQueueHostedService(IServiceScopeFactory scopes, IAiCenterConfiguration configuration,
    ILogger<AiRunQueueHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var running = new List<Task>();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                running.RemoveAll(t => t.IsCompleted);
                if (configuration.Enabled && running.Count < configuration.RunWorkerConcurrency)
                {
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        using var system = scope.ServiceProvider.GetRequiredService<ISystemTenantScope>().Begin(SystemTenantOperations.AiRunQueue);
                        var store = scope.ServiceProvider.GetRequiredService<IAiRunExecutionStore>();
                        var pending = await store.ListPendingAsync(configuration.RunWorkerConcurrency - running.Count, stoppingToken);
                        foreach (var item in pending) running.Add(ProcessAsync(item.TenantId, item.RunId, stoppingToken));
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch { logger.LogWarning("AI queue discovery failed; it will be retried."); }
                }
                await Task.Delay(TimeSpan.FromSeconds(configuration.RunQueueIntervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { await Task.WhenAll(running); }
    }

    internal async Task ProcessAsync(Guid tenantId, Guid runId, CancellationToken token)
    {
        Guid? leaseId = null;
        try
        {
            await using var claimScope = scopes.CreateAsyncScope();
            claimScope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId, "Request");
            var run = await claimScope.ServiceProvider.GetRequiredService<IAiRunExecutionStore>().ClaimAsync(runId, token);
            if (run is null) return;
            leaseId = run.ExecutionLeaseId;
            await using var executionScope = scopes.CreateAsyncScope();
            var tenant = executionScope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenant.SetTenant(tenantId, "Request");
            var actor = await executionScope.ServiceProvider.GetRequiredService<AiRunIdentityValidator>().ValidateAsync(run, token);
            executionScope.ServiceProvider.GetRequiredService<AiRunExecutionIdentity>().Set(actor, run.ActorSessionId!);
            tenant.MarkAsSuperAdmin(executionScope.ServiceProvider.GetRequiredService<AiRunExecutionIdentity>().IsSuperAdmin);
            using var heartbeatToken = CancellationTokenSource.CreateLinkedTokenSource(token);
            using var executionToken = CancellationTokenSource.CreateLinkedTokenSource(token);
            var heartbeat = HeartbeatAsync(tenantId, runId, leaseId.Value, executionToken, heartbeatToken.Token);
            try { await executionScope.ServiceProvider.GetRequiredService<IAiRunExecutionService>().ExecuteAsync(runId, leaseId.Value, executionToken.Token); }
            finally { heartbeatToken.Cancel(); await heartbeat; }
        }
        catch (AiRunLeaseLostException) { }
        catch (Exception)
        {
            // Error details may include provider or database secrets; report only controlled identifiers.
            logger.LogWarning("AI background execution stopped. RunId={RunId}", runId);
        }
        finally
        {
            if (leaseId.HasValue)
            {
                try
                {
                    await using var recovery = scopes.CreateAsyncScope();
                    recovery.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId, "Request");
                    await recovery.ServiceProvider.GetRequiredService<IAiRunExecutionStore>().TerminateAsync(runId, leaseId.Value,
                        token.IsCancellationRequested ? "worker_stopped" : "run_failed", CancellationToken.None);
                }
                catch { logger.LogWarning("AI Run requires watchdog recovery. RunId={RunId}", runId); }
            }
        }
    }

    private async Task HeartbeatAsync(Guid tenantId, Guid runId, Guid leaseId, CancellationTokenSource execution,
        CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(configuration.RunHeartbeatIntervalSeconds));
            while (await timer.WaitForNextTickAsync(token))
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId, "Request");
                if (!await scope.ServiceProvider.GetRequiredService<IAiRunExecutionStore>().HeartbeatAsync(runId, leaseId, token))
                { execution.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { execution.Cancel(); }
    }
}
