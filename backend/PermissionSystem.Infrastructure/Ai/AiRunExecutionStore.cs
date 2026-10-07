using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiRunExecutionStore(AppDbContext db, ITenantContext tenant, IAiCenterConfiguration configuration)
    : IAiRunExecutionStore
{
    public Task<AiRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default) =>
        db.AiRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

    public async Task<IReadOnlyList<(Guid TenantId, Guid RunId)>> ListPendingAsync(int limit,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSystemScopeActive) throw new InvalidOperationException("Queue discovery requires a system scope.");
        var rows = await db.AiRuns.AsNoTracking().Where(r => r.ExecutionMode == "Background" && r.Status == AiRunStatus.Pending)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Take(Math.Clamp(limit, 1, 100))
            .Select(r => new { r.TenantId, r.Id }).ToListAsync(cancellationToken);
        return rows.Select(r => (r.TenantId, r.Id)).ToArray();
    }

    internal static Task<AiRun?> LockAsync(AppDbContext db, Guid tenantId, Guid id, CancellationToken token) =>
        (db.Database.IsSqlServer()
            ? db.AiRuns.FromSqlInterpolated($"SELECT * FROM ai_run WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = {tenantId} AND Id = {id} AND IsDeleted = 0")
            : db.AiRuns.Where(r => r.TenantId == tenantId && r.Id == id))
        .AsNoTracking().FirstOrDefaultAsync(token);

    private async Task<T> MutateAsync<T>(Guid runId, Func<AiRun?, Task<T>> action, CancellationToken token)
    {
        if (!tenant.TenantId.HasValue) throw new InvalidOperationException("An explicit tenant is required.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(token) : null;
        var run = await LockAsync(db, tenant.TenantId.Value, runId, token);
        var result = await action(run);
        if (transaction is not null) await transaction.CommitAsync(token);
        return result;
    }

    public Task<AiRun?> ClaimAsync(Guid runId, CancellationToken cancellationToken = default) =>
        MutateAsync<AiRun?>(runId, async run =>
        {
            var now = DateTimeOffset.UtcNow;
            if (!configuration.Enabled || run is null || run.ExecutionMode != "Background" || run.Status != AiRunStatus.Pending ||
                run.CancellationRequestedAt.HasValue || run.QueueDeadlineAt is null || run.QueueDeadlineAt <= now) return null;
            run.Status = AiRunStatus.Running; run.ExecutionLeaseId = Guid.NewGuid();
            run.StartedAt = now; run.LastHeartbeatAt = now;
            // Scenario duration is validated by the executor; the admitted duration is encoded in DeadlineAt.
            var duration = run.DeadlineAt.HasValue ? run.DeadlineAt.Value - run.CreatedAt : TimeSpan.FromSeconds(90);
            run.DeadlineAt = now + duration;
            run.ProgressVersion = (run.ProgressVersion ?? 0) + 1;
            db.AiRuns.Update(run); await db.SaveChangesAsync(cancellationToken); db.Entry(run).State = EntityState.Detached;
            return run;
        }, cancellationToken);

    public Task<bool> HeartbeatAsync(Guid runId, Guid leaseId, CancellationToken cancellationToken = default) =>
        MutateAsync(runId, async run =>
        {
            if (run is null || run.Status != AiRunStatus.Running || run.ExecutionLeaseId != leaseId ||
                run.CancellationRequestedAt.HasValue || run.DeadlineAt <= DateTimeOffset.UtcNow) return false;
            run.LastHeartbeatAt = DateTimeOffset.UtcNow;
            db.AiRuns.Attach(run); db.Entry(run).Property(r => r.LastHeartbeatAt).IsModified = true;
            await db.SaveChangesAsync(cancellationToken); db.Entry(run).State = EntityState.Detached;
            return true;
        }, cancellationToken);

    public async Task CancelAsync(Guid runId, CancellationToken cancellationToken = default) =>
        await MutateAsync(runId, async run =>
        {
            if (run is null || run.Status is not (AiRunStatus.Pending or AiRunStatus.Running)) return false;
            run.CancellationRequestedAt ??= DateTimeOffset.UtcNow;
            if (run.Status == AiRunStatus.Pending) Finish(run, AiRunStatus.Cancelled, "run_cancelled");
            run.ProgressVersion = (run.ProgressVersion ?? 0) + 1;
            db.AiRuns.Update(run); await db.SaveChangesAsync(cancellationToken); db.Entry(run).State = EntityState.Detached;
            return true;
        }, cancellationToken);

    public async Task TerminateAsync(Guid runId, Guid leaseId, string errorCode, CancellationToken cancellationToken = default) =>
        await MutateAsync(runId, async run =>
        {
            if (run is null || run.ExecutionLeaseId != leaseId || run.Status is not (AiRunStatus.Pending or AiRunStatus.Running)) return false;
            if (errorCode == "run_orphaned")
            {
                var now = DateTimeOffset.UtcNow;
                var expired = run.ExecutionMode == "Background" && run.Status == AiRunStatus.Pending
                    ? run.QueueDeadlineAt <= now
                    : run.DeadlineAt <= now || (run.LastHeartbeatAt ?? run.StartedAt ?? run.CreatedAt) < now.AddSeconds(-Math.Max(30, configuration.RunOrphanTimeoutSeconds));
                if (!expired) return false;
                if (run.Status == AiRunStatus.Pending && run.ExecutionMode == "Background") errorCode = "run_queue_timeout";
            }
            Finish(run, run.CancellationRequestedAt.HasValue ? AiRunStatus.Cancelled : AiRunStatus.Failed,
                run.CancellationRequestedAt.HasValue ? "run_cancelled" : errorCode);
            run.ExecutionLeaseId = Guid.NewGuid(); run.ProgressVersion = (run.ProgressVersion ?? 0) + 1;
            db.AiRuns.Update(run);
            var usages = await db.AiUsageLogs.Where(u => u.RunId == runId && u.Status == AiInvocationStatus.Running).ToListAsync(cancellationToken);
            foreach (var usage in usages)
            {
                usage.SettleCost(); usage.Status = AiInvocationStatus.Failed; usage.ErrorCode = run.ErrorCode;
                usage.CompletedAt = run.CompletedAt;
            }
            var tools = await db.AiToolInvocations.Where(t => t.RunId == runId && t.Status == AiInvocationStatus.Running).ToListAsync(cancellationToken);
            foreach (var tool in tools)
            {
                tool.Status = run.Status == AiRunStatus.Cancelled ? AiInvocationStatus.Cancelled : AiInvocationStatus.Failed;
                tool.ErrorCode = run.ErrorCode; tool.CompletedAt = run.CompletedAt;
            }
            await db.SaveChangesAsync(cancellationToken); db.ChangeTracker.Clear(); return true;
        }, cancellationToken);

    private static void Finish(AiRun run, AiRunStatus status, string code)
    {
        run.Status = status; run.ErrorCode = code; run.ErrorSummary = "The AI run stopped before completion.";
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.DurationMilliseconds = Math.Max(0, (long)(run.CompletedAt.Value - (run.StartedAt ?? run.CreatedAt)).TotalMilliseconds);
    }
}
