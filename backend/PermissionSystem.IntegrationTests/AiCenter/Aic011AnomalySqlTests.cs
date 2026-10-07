using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiAnomalies;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Messaging;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic011AnomalySqlTests
{
    private const string ConnectionVariable = "PERMISSION_SYSTEM_AIC011_SQL_TEST_CONNECTION";
    private const string IsolatedVariable = "PERMISSION_SYSTEM_AIC011_SQL_TEST_ISOLATED";

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task CompositeTaskForeignKey_RejectsCrossTenantRule()
    {
        await using var db = Context(Guid.NewGuid()); await RequireSchema(db);
        await using var transaction = await db.Database.BeginTransactionAsync(); var seed = await Seed(db);
        var tenant = new TenantContext(); tenant.SetTenant(Guid.NewGuid(), "Synthetic SQL fixture");
        await using var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(db.Database.GetDbConnection()).Options, tenant, new NullAuditContext());
        await other.Database.UseTransactionAsync(transaction.GetDbTransaction());
        other.AiAnomalyRules.Add(new() { TenantId = tenant.TenantId!.Value, OwnerUserId = Guid.NewGuid(), ScheduledTaskId = seed.Task.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task DuplicateEpisode_IsRejectedByDatabase()
    {
        await using var db = Context(Guid.NewGuid()); await RequireSchema(db);
        await using var transaction = await db.Database.BeginTransactionAsync(); var seed = await Seed(db);
        db.AiAnomalyEvents.Add(Event(seed.Rule)); await db.SaveChangesAsync();
        db.AiAnomalyEvents.Add(Event(seed.Rule)); await Assert.ThrowsAsync<BusinessException>(() => db.SaveChangesAsync());
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task NotificationKey_SurvivesSoftDeletionAndLookupPreventsReplay()
    {
        await using var db = Context(Guid.NewGuid()); await RequireSchema(db);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var key = $"aic011-{Guid.NewGuid():N}";
        var notification = new Notification { TenantId = db.CurrentTenantId!.Value, DeliveryKey = key,
            Title = "Synthetic generic notification", Content = "No business evidence", Type = "Task", IsDeleted = true };
        db.Notifications.Add(notification); await db.SaveChangesAsync();
        Assert.Equal(notification.Id, (await new ControlledNotificationLookup(db).FindAsync(notification.TenantId, key, default))!.NotificationId);
        db.Notifications.Add(new() { TenantId = notification.TenantId, DeliveryKey = key, Title = "Duplicate", Content = "Duplicate", Type = "Task" });
        await Assert.ThrowsAsync<BusinessException>(() => db.SaveChangesAsync());
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task StaleRuleWrite_ConflictsAndFenceReloadsMetadata()
    {
        await using var db = Context(Guid.NewGuid()); await RequireSchema(db);
        await using var transaction = await db.Database.BeginTransactionAsync(); var seed = await Seed(db);
        var role = new Role { Id = Guid.NewGuid(), TenantId = seed.Rule.TenantId, Code = $"aic011-{Guid.NewGuid():N}", Name = "Synthetic scope", IsEnabled = true };
        var scope = new RoleDataScope { TenantId = seed.Rule.TenantId, RoleId = role.Id, ScopeType = DataScopeType.All };
        db.Roles.Add(role); db.RoleDataScopes.Add(scope); await db.SaveChangesAsync();
        await db.RoleDataScopes.Where(s => s.Id == scope.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.ScopeType, DataScopeType.CurrentUser));
        await db.AiAnomalyRules.Where(r => r.Id == seed.Rule.Id).ExecuteUpdateAsync(set => set.SetProperty(r => r.IsEnabled, true));
        await new AiAnomalyCommitFence(db).HoldAsync(seed.Rule.Id, default);
        Assert.True(seed.Rule.IsEnabled); Assert.Equal(DataScopeType.CurrentUser, scope.ScopeType);
        await db.AiAnomalyRules.Where(r => r.Id == seed.Rule.Id).ExecuteUpdateAsync(set => set.SetProperty(r => r.IsEnabled, false));
        seed.Rule.IsEnabled = false;
        db.AiAnomalyRules.Update(seed.Rule);
        await Assert.ThrowsAsync<BusinessException>(() => db.SaveChangesAsync());
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task SourceAndEffectTransaction_RollsBackReceiptNotificationAndCooldownTogether()
    {
        await using var db = Context(Guid.NewGuid()); await RequireSchema(db);
        Guid ruleId;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var seed = await Seed(db); ruleId = seed.Rule.Id;
            var item = Event(seed.Rule); item.DeliveryStatus = AiAnomalyDeliveryStatus.Delivered;
            seed.Rule.LastNotifiedAt = DateTimeOffset.UtcNow;
            db.AiAnomalyEvents.Add(item); db.Notifications.Add(new() { TenantId = seed.Rule.TenantId,
                DeliveryKey = item.DeliveryKey, Title = "Synthetic generic reminder", Content = "Generic content", Type = "Task" });
            await db.SaveChangesAsync(); await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear(); Assert.False(await db.AiAnomalyRules.AnyAsync(r => r.Id == ruleId));
        Assert.Empty(await db.AiAnomalyEvents.ToListAsync()); Assert.Empty(await db.Notifications.ToListAsync());
    }

    private static AppDbContext Context(Guid tenantId)
    {
        var tenant = new TenantContext(); tenant.SetTenant(tenantId, "Synthetic SQL fixture");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Environment.GetEnvironmentVariable(ConnectionVariable)!).Options,
            tenant, new NullAuditContext());
    }
    private static async Task RequireSchema(AppDbContext db) => Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    private static async Task<(AiAnomalyRule Rule, ScheduledTask Task)> Seed(AppDbContext db)
    {
        var tenantId = db.CurrentTenantId!.Value;
        db.Tenants.Add(new() { Id = tenantId, TenantId = tenantId, Code = $"aic011-{tenantId:N}", Name = "Synthetic SQL fixture" });
        var task = new ScheduledTask { Id = Guid.NewGuid(), TenantId = tenantId, Code = $"aic011-{Guid.NewGuid():N}", Name = "Synthetic reminder",
            JobType = AiAnomalyContract.JobType, CronExpression = AiAnomalyContract.Cron, Queue = "default", IsEnabled = false };
        var rule = new AiAnomalyRule { Id = Guid.NewGuid(), TenantId = tenantId, OwnerUserId = Guid.NewGuid(), ScheduledTaskId = task.Id };
        db.ScheduledTasks.Add(task); db.AiAnomalyRules.Add(rule); await db.SaveChangesAsync(); return (rule, task);
    }
    private static AiAnomalyEvent Event(AiAnomalyRule rule)
    {
        var id = Guid.NewGuid();
        return new() { Id = id, TenantId = rule.TenantId, RuleId = rule.Id, RecipientUserId = rule.OwnerUserId,
            EpisodeSequence = 1, ObservedCount = 1, ObservedAt = DateTimeOffset.UtcNow,
            ScopeFingerprint = new string('A', 64), DeliveryKey = $"aic011-{id:N}" };
    }
    private sealed class SqlFactAttribute : FactAttribute
    {
        public SqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)) ||
                Environment.GetEnvironmentVariable(IsolatedVariable) != "1")
                Skip = "Requires explicitly isolated AIC-011 SQL Server with reviewed/applied migrations. Synthetic fixtures are rolled back; no migration is applied by tests.";
        }
    }
}
