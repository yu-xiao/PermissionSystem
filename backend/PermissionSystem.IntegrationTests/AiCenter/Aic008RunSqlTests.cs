using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic008RunSqlTests
{
    private const string ConnectionVariable = "PERMISSION_SYSTEM_AIC008_SQL_TEST_CONNECTION";

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task ConcurrentClaim_ShouldAllowExactlyOneWorkerAndHeartbeatCannotExtendDeadline()
    {
        var seed = await SeedAsync();
        try
        {
            await using var a = Context(seed.Tenant); await using var b = Context(seed.Tenant);
            var claimed = await Task.WhenAll(Store(a, seed.Tenant).ClaimAsync(seed.Run), Store(b, seed.Tenant).ClaimAsync(seed.Run));
            var winner = Assert.Single(claimed, r => r is not null)!;
            Assert.Equal(AiRunStatus.Running, winner.Status);
            await using var verify = Context(seed.Tenant);
            Assert.True(await Store(verify, seed.Tenant).HeartbeatAsync(seed.Run, winner.ExecutionLeaseId));
            Assert.Equal(winner.DeadlineAt, (await verify.AiRuns.AsNoTracking().SingleAsync()).DeadlineAt);
        }
        finally { await CleanupAsync(seed.Tenant); }
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task CancellationBeforeCommit_ShouldFenceLateAssistantAndPreserveTerminalRun()
    {
        var seed = await SeedAsync();
        try
        {
            await using var controller = Context(seed.Tenant);
            var run = (await Store(controller, seed.Tenant).ClaimAsync(seed.Run))!;
            var fence = new AiRunExecutionFence(); fence.Begin(run.Id, run.ExecutionLeaseId);
            await using var worker = Context(seed.Tenant, fence);
            await worker.AiRuns.SingleAsync();
            await Store(controller, seed.Tenant).CancelAsync(run.Id);
            worker.AiMessages.Add(new() { TenantId = seed.Tenant, ConversationId = seed.Conversation,
                Role = AiMessageRole.Assistant, Content = "Synthetic late answer", ContentDigest = "synthetic", Sequence = 2 });
            await Assert.ThrowsAsync<OperationCanceledException>(() => worker.SaveChangesAsync());
            await Store(controller, seed.Tenant).TerminateAsync(run.Id, run.ExecutionLeaseId, "worker_stopped");
            await using var verify = Context(seed.Tenant);
            Assert.Empty(verify.AiMessages.Where(m => m.Role == AiMessageRole.Assistant));
            Assert.Equal(AiRunStatus.Cancelled, (await verify.AiRuns.SingleAsync()).Status);
        }
        finally { await CleanupAsync(seed.Tenant); }
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task MessageAndRunTransaction_ShouldRollbackAllWritesOnFailure()
    {
        var seed = await SeedAsync();
        try
        {
            await using var db = Context(seed.Tenant);
            var messageId = Guid.NewGuid();
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                db.AiMessages.Add(new() { Id = messageId, TenantId = seed.Tenant, ConversationId = seed.Conversation,
                    Role = AiMessageRole.User, Content = "Synthetic aborted submission", ContentDigest = "synthetic", Sequence = 2 });
                await db.SaveChangesAsync();
                // A second active Run must fail the unique constraint; transaction disposal rolls back the saved message.
                var existing = await db.AiRuns.AsNoTracking().SingleAsync();
                existing.Id = Guid.NewGuid(); existing.RequestMessageId = messageId; existing.RowVersion = [];
                db.AiRuns.Add(existing);
                await Assert.ThrowsAsync<PermissionSystem.Shared.Exceptions.BusinessException>(() => db.SaveChangesAsync());
            }
            await using var verify = Context(seed.Tenant);
            Assert.False(await verify.AiMessages.AnyAsync(m => m.Id == messageId));
            Assert.Single(verify.AiRuns);
        }
        finally { await CleanupAsync(seed.Tenant); }
    }

    private static AppDbContext Context(Guid tenantId, AiRunExecutionFence? fence = null)
    {
        var tenant = new TenantContext(); tenant.SetTenant(tenantId, "Request");
        return new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable(ConnectionVariable)!).Options, tenant, new NullAuditContext(), fence);
    }
    private static AiRunExecutionStore Store(AppDbContext db, Guid tenantId)
    {
        var tenant = new TenantContext(); tenant.SetTenant(tenantId, "Request");
        return new(db, tenant, new AiCenterOptions { Enabled = true, AllowedTenantIds = [tenantId] });
    }
    private static async Task<Seed> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context(tenantId);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var user = new User { Id = Guid.NewGuid(), TenantId = tenantId, UserName = "aic008-synthetic", NormalizedUserName = "AIC008-SYNTHETIC" };
        var provider = new AiProviderConfig { Id = Guid.NewGuid(), TenantId = tenantId, ProviderCode = "synthetic",
            ProviderName = "Synthetic", BaseUrl = "https://evaluation.invalid", ModelName = "synthetic", ApiKeyEncrypted = "unusable-synthetic-fixture" };
        var conversation = new AiConversation { Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id,
            Title = "Synthetic AIC-008 SQL fixture", AgentCode = "synthetic", AgentVersion = "1",
            LastMessageAt = DateTimeOffset.UtcNow, RetentionUntil = DateTimeOffset.UtcNow.AddDays(1) };
        var message = new AiMessage { Id = Guid.NewGuid(), TenantId = tenantId, ConversationId = conversation.Id,
            Role = AiMessageRole.User, Content = "Synthetic", ContentDigest = "synthetic", Sequence = 1 };
        var run = new AiRun { Id = Guid.NewGuid(), TenantId = tenantId, ConversationId = conversation.Id,
            RequestMessageId = message.Id, ProviderConfigId = provider.Id, ActorUserId = user.Id, ExecutionMode = "Background",
            ActorSessionId = "synthetic-reference", ActorSecurityStamp = Guid.NewGuid(), ExecutionLeaseId = Guid.NewGuid(),
            ProgressVersion = 1, QueueDeadlineAt = DateTimeOffset.UtcNow.AddMinutes(5), DeadlineAt = DateTimeOffset.UtcNow.AddSeconds(90),
            AgentCode = "synthetic", AgentVersion = "1", PromptVersion = "1", ModelName = "synthetic", TraceId = "synthetic" };
        db.Tenants.Add(new() { Id = tenantId, TenantId = tenantId, Code = $"aic008-{tenantId:N}", Name = "Synthetic SQL fixture" });
        db.Users.Add(user); db.AiProviderConfigs.Add(provider); db.AiConversations.Add(conversation); db.AiMessages.Add(message); db.AiRuns.Add(run);
        await db.SaveChangesAsync(); return new(tenantId, run.Id, conversation.Id);
    }
    private static async Task CleanupAsync(Guid tenantId)
    {
        // Only this test's freshly generated tenant is eligible for fixture cleanup.
        await using var db = Context(tenantId);
        await db.AiRuns.Where(r => r.TenantId == tenantId).ExecuteDeleteAsync();
        await db.AiMessages.Where(r => r.TenantId == tenantId).ExecuteDeleteAsync();
        await db.AiConversations.Where(r => r.TenantId == tenantId).ExecuteDeleteAsync();
        await db.AiProviderConfigs.Where(r => r.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Users.Where(r => r.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Tenants.Where(r => r.Id == tenantId).ExecuteDeleteAsync();
    }
    private sealed record Seed(Guid Tenant, Guid Run, Guid Conversation);
    private sealed class SqlFactAttribute : FactAttribute
    {
        public SqlFactAttribute()
        { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable))) Skip = "An isolated AIC-008 SQL Server with reviewed migrations is required."; }
    }
}
