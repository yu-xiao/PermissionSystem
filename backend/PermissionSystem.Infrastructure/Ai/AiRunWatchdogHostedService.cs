using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.Infrastructure.Ai;

/// <summary>
/// Reclaims AI runs left behind by a crashed API instance and settles their
/// budget reservations conservatively. This worker is deliberately hosted by the Worker
/// process so API replicas do not all scan the same tables.
/// </summary>
public sealed class AiRunWatchdogHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiRunWatchdogHostedService> _logger;
    private readonly AiCenterOptions _options;

    public AiRunWatchdogHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<AiCenterOptions> options,
        ILogger<AiRunWatchdogHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.RunWatchdogIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ReclaimAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                _logger.LogError("AI Run watchdog failed.");
            }
        }
    }

    internal async Task ReclaimAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var systemScope = scope.ServiceProvider.GetRequiredService<ISystemTenantScope>();
        var distributedLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var tenantScope = systemScope.Begin(SystemTenantOperations.AiRunWatchdog);
        await distributedLock.ExecuteWithLockAsync(
            "ai:run-watchdog",
            async token =>
            {
                var now = DateTimeOffset.UtcNow;
                var cutoff = now.AddSeconds(-Math.Max(30, _options.RunOrphanTimeoutSeconds));
                var orphaned = await dbContext.AiRuns
                    .IgnoreQueryFilters()
                    .Where(run => !run.IsDeleted &&
                        (run.Status == AiRunStatus.Pending || run.Status == AiRunStatus.Running) &&
                        ((run.ExecutionMode == "Background" && run.Status == AiRunStatus.Pending && run.QueueDeadlineAt < now) ||
                         ((run.ExecutionMode != "Background" || run.Status != AiRunStatus.Pending) &&
                          ((run.DeadlineAt.HasValue && run.DeadlineAt < now) ||
                           (run.LastHeartbeatAt ?? run.StartedAt ?? run.CreatedAt) < cutoff))))
                    .AsNoTracking().Take(100)
                    .ToListAsync(token);
                if (orphaned.Count == 0)
                {
                    return;
                }

                foreach (var run in orphaned)
                {
                    await using var recovery = _scopeFactory.CreateAsyncScope();
                    var tenant = recovery.ServiceProvider.GetRequiredService<ITenantContext>();
                    tenant.SetTenant(run.TenantId, "Request");
                    var store = new AiRunExecutionStore(recovery.ServiceProvider.GetRequiredService<AppDbContext>(), tenant, _options);
                    await store.TerminateAsync(run.Id, run.ExecutionLeaseId, "run_orphaned", token);
                }
                _logger.LogWarning("Reclaimed {Count} orphaned AI runs.", orphaned.Count);
            },
            expiry: TimeSpan.FromSeconds(20),
            waitTime: TimeSpan.Zero,
            cancellationToken: cancellationToken);
    }
}
