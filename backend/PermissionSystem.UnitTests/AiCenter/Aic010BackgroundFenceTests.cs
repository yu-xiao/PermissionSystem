using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed partial class Aic008BackgroundRunTests
{
    [Fact]
    public async Task Aic010_CancellationSettlement_DiscardsUnsavedKnowledgeDependencies()
    {
        await using var f = await Fixture.CreateAsync();
        var run = (await f.Store.ClaimAsync(f.RunId))!;
        await f.Store.CancelAsync(run.Id);
        f.Fence.Begin(run.Id, run.ExecutionLeaseId);
        f.Db.AiKnowledgeRunReferences.Add(new() { TenantId = run.TenantId, RunId = run.Id,
            InvocationId = "synthetic", DocumentId = Guid.NewGuid(), VersionId = Guid.NewGuid(), ChunkId = Guid.NewGuid(), ContentHash = new string('A', 64) });
        await Assert.ThrowsAsync<OperationCanceledException>(() => f.Db.SaveChangesAsync());
        f.Fence.IsSettlement = true;
        (await f.Db.AiRuns.SingleAsync()).Status = AiRunStatus.Failed;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
        Assert.Empty(f.Db.AiKnowledgeRunReferences);
        Assert.Equal(AiRunStatus.Cancelled, (await f.Store.FindAsync(run.Id))!.Status);
    }

    [Fact]
    public async Task Aic010_LostLease_RejectsKnowledgeDependencies()
    {
        await using var f = await Fixture.CreateAsync();
        var run = (await f.Store.ClaimAsync(f.RunId))!;
        await f.Store.TerminateAsync(run.Id, run.ExecutionLeaseId, "worker_stopped");
        f.Fence.Begin(run.Id, run.ExecutionLeaseId);
        f.Db.AiKnowledgeRunReferences.Add(new() { TenantId = run.TenantId, RunId = run.Id,
            InvocationId = "synthetic", DocumentId = Guid.NewGuid(), VersionId = Guid.NewGuid(), ChunkId = Guid.NewGuid(), ContentHash = new string('A', 64) });
        await Assert.ThrowsAsync<AiRunLeaseLostException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); Assert.Empty(f.Db.AiKnowledgeRunReferences);
    }
}
