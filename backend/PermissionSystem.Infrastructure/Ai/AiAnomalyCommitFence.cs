using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.AiAnomalies;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiAnomalyCommitFence(AppDbContext db) : IAiAnomalyCommitFence
{
    public async Task HoldAsync(Guid ruleId, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is null || db.IsSystemTenantScopeActive ||
            db.CurrentTenantId is not Guid tenantId)
            throw new InvalidOperationException("Reminder commits require a tenant SQL transaction.");
        await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [Tenants] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId} AND [Id] = {tenantId}").ToListAsync(ct);
        var owners = await db.Database.SqlQuery<Guid>($"SELECT [OwnerUserId] AS [Value] FROM [ai_anomaly_rule] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId} AND [Id] = {ruleId}").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"""
            SELECT t.[Id] AS [Value] FROM [ScheduledTasks] t WITH (UPDLOCK, HOLDLOCK)
            JOIN [ai_anomaly_rule] r ON r.[TenantId] = t.[TenantId] AND r.[ScheduledTaskId] = t.[Id]
            WHERE r.[TenantId] = {tenantId} AND r.[Id] = {ruleId}
            """).ToListAsync(ct);
        foreach (var owner in owners)
        {
            await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [Users] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId} AND [Id] = {owner}").ToListAsync(ct);
            await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [UserRoles] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId} AND [UserId] = {owner}").ToListAsync(ct);
            await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [UserDataScopes] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId} AND [UserId] = {owner}").ToListAsync(ct);
        }
        await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [Roles] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId}").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [RoleDataScopes] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId}").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [RolePermissions] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId}").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [Permissions] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId}").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [Departments] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId}").ToListAsync(ct);
        // A context may have read metadata before waiting for the commit fence.
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Unchanged &&
            e.Entity is AiAnomalyRule or AiAnomalyEvent or ScheduledTask or User or Role or UserRole or UserDataScope or
                RoleDataScope or Department or Tenant).ToArray())
            await entry.ReloadAsync(ct);
    }
}
