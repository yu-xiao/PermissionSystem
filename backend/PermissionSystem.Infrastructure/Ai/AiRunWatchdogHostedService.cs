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
/// Reclaims AI runs left behind by a crashed API instance and releases their
/// budget reservations. This worker is deliberately hosted by the Worker
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
            catch (Exception exception)
            {
                _logger.LogError(exception, "AI Run watchdog failed.");
            }
        }
    }

    private async Task ReclaimAsync(CancellationToken cancellationToken)
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
                        ((run.DeadlineAt.HasValue && run.DeadlineAt < now) ||
                         (run.LastHeartbeatAt ?? run.StartedAt ?? run.CreatedAt) < cutoff))
                    .ToListAsync(token);
                if (orphaned.Count == 0)
                {
                    return;
                }

                var ids = orphaned.Select(run => run.Id).ToArray();
                foreach (var run in orphaned)
                {
                    // Rotate the lease so an old API instance cannot persist a
                    // late completion after this watchdog has reclaimed the run.
                    run.ExecutionLeaseId = Guid.NewGuid();
                    run.Status = AiRunStatus.Failed;
                    run.ErrorCode = "run_orphaned";
                    run.ErrorSummary = "The AI run was reclaimed after its worker stopped reporting progress.";
                    run.CompletedAt = now;
                    run.DurationMilliseconds = Math.Max(0, (long)(now - (run.StartedAt ?? run.CreatedAt)).TotalMilliseconds);
                    run.CancellationRequestedAt ??= now;
                    run.UpdatedAt = now;
                }

                await dbContext.AiUsageLogs
                    .IgnoreQueryFilters()
                    .Where(log => ids.Contains(log.RunId) &&
                        log.ReservedCost.HasValue &&
                        log.Status == AiInvocationStatus.Running)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(log => log.ReservedCost, (decimal?)null)
                        .SetProperty(log => log.ReservationExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(log => log.Status, AiInvocationStatus.Failed)
                        .SetProperty(log => log.ErrorCode, "run_orphaned")
                        .SetProperty(log => log.CompletedAt, now), token);

                await dbContext.SaveChangesAsync(token);
                _logger.LogWarning("Reclaimed {Count} orphaned AI runs.", orphaned.Count);
            },
            expiry: TimeSpan.FromSeconds(20),
            waitTime: TimeSpan.Zero,
            cancellationToken: cancellationToken);
    }
}
