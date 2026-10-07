using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiKnowledgeCommitFence(AppDbContext db, ICurrentUserService current) : IAiKnowledgeCommitFence
{
    public async Task HoldDocumentAsync(Guid documentId, CancellationToken ct)
    {
        var tenantId = RequireTransaction();
        await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [ai_knowledge_document] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId} AND [Id] = {documentId}").ToListAsync(ct);
    }

    public async Task HoldCapacityAsync(Guid tenantId, CancellationToken ct)
    {
        if (RequireTransaction() != tenantId) throw new InvalidOperationException("Knowledge capacity tenant mismatch.");
        await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [Tenants] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId} AND [Id] = {tenantId}").ToListAsync(ct);
    }

    private Guid RequireTransaction()
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is null || db.IsSystemTenantScopeActive ||
            current.TenantId != db.CurrentTenantId || current.TenantId is not Guid tenantId)
            throw new InvalidOperationException("Knowledge evidence requires a tenant SQL transaction.");
        return tenantId;
    }

    public async Task HoldAsync(Guid runId, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is null || db.IsSystemTenantScopeActive ||
            current.TenantId != db.CurrentTenantId || current.UserId is not Guid actorId || current.TenantId is not Guid tenantId)
            throw new InvalidOperationException("Knowledge evidence requires a tenant SQL transaction.");
        // Hold source and authorization rows only for the final commit, never across model I/O.
        await db.Database.SqlQuery<Guid>($"""
            SELECT DISTINCT d.[Id] AS [Value]
            FROM [ai_knowledge_document] d WITH (UPDLOCK, HOLDLOCK)
            JOIN [ai_knowledge_run_reference] x ON x.[TenantId] = d.[TenantId] AND x.[DocumentId] = d.[Id]
            WHERE x.[TenantId] = {tenantId} AND x.[RunId] = {runId}
            """).ToListAsync(ct);
        await HoldIdentityAsync(tenantId, actorId, ct);
        await db.Database.SqlQuery<Guid>($"""
            SELECT DISTINCT f.[Id] AS [Value] FROM [FileResources] f WITH (UPDLOCK, HOLDLOCK)
            JOIN [ai_knowledge_document_version] v WITH (UPDLOCK, HOLDLOCK)
                ON v.[TenantId] = f.[TenantId] AND v.[FileResourceId] = f.[Id]
            JOIN [ai_knowledge_run_reference] x ON x.[TenantId] = v.[TenantId] AND x.[VersionId] = v.[Id]
            WHERE x.[TenantId] = {tenantId} AND x.[RunId] = {runId}
            """).ToListAsync(ct);
    }

    public async Task HoldPublicationAsync(Guid documentId, Guid versionId, CancellationToken ct)
    {
        var tenantId = RequireTransaction();
        if (current.UserId is not Guid actorId) throw new InvalidOperationException("Knowledge actor is required.");
        await HoldDocumentAsync(documentId, ct);
        await HoldIdentityAsync(tenantId, actorId, ct);
        await db.Database.SqlQuery<Guid>($"""
            SELECT f.[Id] AS [Value] FROM [FileResources] f WITH (UPDLOCK, HOLDLOCK)
            JOIN [ai_knowledge_document_version] v WITH (UPDLOCK, HOLDLOCK)
                ON v.[TenantId] = f.[TenantId] AND v.[FileResourceId] = f.[Id]
            WHERE v.[TenantId] = {tenantId} AND v.[DocumentId] = {documentId} AND v.[Id] = {versionId}
            """).ToListAsync(ct);
    }

    private async Task HoldIdentityAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [Tenants] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId} AND [Id] = {tenantId}").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [Users] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId} AND [Id] = {actorId}").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"""
            SELECT m.[Id] AS [Value] FROM [UserRoles] m WITH (UPDLOCK, HOLDLOCK)
            WHERE m.[TenantId] = {tenantId} AND m.[UserId] = {actorId}
            """).ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"""
            SELECT DISTINCT r.[Id] AS [Value] FROM [Roles] r WITH (UPDLOCK, HOLDLOCK)
            JOIN [UserRoles] m ON m.[TenantId] = r.[TenantId] AND m.[RoleId] = r.[Id]
            WHERE m.[TenantId] = {tenantId} AND m.[UserId] = {actorId}
            """).ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"""
            SELECT DISTINCT p.[Id] AS [Value] FROM [Permissions] p WITH (UPDLOCK, HOLDLOCK)
            JOIN [RolePermissions] g WITH (UPDLOCK, HOLDLOCK) ON g.[TenantId] = p.[TenantId] AND g.[PermissionId] = p.[Id]
            JOIN [UserRoles] m ON m.[TenantId] = g.[TenantId] AND m.[RoleId] = g.[RoleId]
            WHERE m.[TenantId] = {tenantId} AND m.[UserId] = {actorId}
            """).ToListAsync(ct);
    }
}
