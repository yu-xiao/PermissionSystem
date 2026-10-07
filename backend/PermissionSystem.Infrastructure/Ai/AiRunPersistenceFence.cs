using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Ai;

internal static class AiRunPersistenceFence
{
    public static async Task ValidateAsync(AppDbContext db, AiRunExecutionFence fence, CancellationToken token)
    {
        if (!fence.RunId.HasValue) return;
        if (!db.CurrentTenantId.HasValue || db.IsSystemTenantScopeActive) throw new AiRunLeaseLostException();
        db.ChangeTracker.DetectChanges();
        var stored = await AiRunExecutionStore.LockAsync(db, db.CurrentTenantId.Value, fence.RunId.Value, token);
        if (stored is null || stored.ExecutionLeaseId != fence.LeaseId || stored.Status != AiRunStatus.Running)
            throw new AiRunLeaseLostException();
        if (!fence.IsSettlement && (stored.CancellationRequestedAt.HasValue || stored.DeadlineAt <= DateTimeOffset.UtcNow))
            throw new OperationCanceledException("The AI run was cancelled or timed out.", token);
        if (fence.IsSettlement)
        {
            foreach (var reference in db.ChangeTracker.Entries<AiKnowledgeRunReference>().Where(e => e.State == EntityState.Added).ToArray())
                reference.State = EntityState.Detached;
            foreach (var message in db.ChangeTracker.Entries<AiMessage>().Where(e => e.State == EntityState.Added).ToArray())
                message.State = EntityState.Detached;
            foreach (var tool in db.ChangeTracker.Entries<AiToolInvocation>().Where(e => e.State is EntityState.Added or EntityState.Modified))
            {
                tool.Entity.Status = stored.CancellationRequestedAt.HasValue ? AiInvocationStatus.Cancelled : AiInvocationStatus.Failed;
                tool.Entity.CitationJson = null; tool.Entity.OutputDigest = null; tool.Entity.RowCount = null;
                tool.Entity.CompletedAt = DateTimeOffset.UtcNow;
            }
        }

        var entry = db.ChangeTracker.Entries<AiRun>().FirstOrDefault(e => e.Entity.Id == stored.Id);
        if (entry is not null)
        {
            var changed = entry.Properties.Where(p => !Equals(p.CurrentValue, p.OriginalValue) &&
                p.Metadata.Name is not (nameof(AiRun.RowVersion) or nameof(AiRun.LastHeartbeatAt) or
                    nameof(AiRun.CancellationRequestedAt) or nameof(AiRun.ProgressVersion) or nameof(AiRun.ExecutionLeaseId)))
                .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
            entry.State = EntityState.Unchanged;
            entry.CurrentValues.SetValues(stored); entry.OriginalValues.SetValues(stored);
            foreach (var pair in changed) entry.Property(pair.Key).CurrentValue = pair.Value;
            entry.Entity.ProgressVersion = (stored.ProgressVersion ?? 0) + 1;
        }
        else
        {
            db.AiRuns.Attach(stored);
            stored.ProgressVersion = (stored.ProgressVersion ?? 0) + 1;
        }
        if (fence.IsSettlement && stored.CancellationRequestedAt.HasValue && entry?.Entity.Status == AiRunStatus.Failed)
        { entry.Entity.Status = AiRunStatus.Cancelled; entry.Entity.ErrorCode = "run_cancelled"; }
    }
}
