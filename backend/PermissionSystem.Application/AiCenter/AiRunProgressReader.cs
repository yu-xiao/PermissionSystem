using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiCenter;

public sealed record AiRunProgressSnapshot(Guid RunId, Guid ConversationId, long Version,
    PermissionSystem.Domain.Enums.AiRunStatus Status, string? ErrorCode, IReadOnlyList<AiToolProgress> Tools);

public sealed class AiRunProgressReader(IAiRunExecutionStore store, IUserCredentialValidator identities,
    IUserSessionStatusChecker sessions, ITenantContext tenant, IAiCenterConfiguration configuration,
    IRepository<AiToolInvocation> tools, IAsyncQueryExecutor queries)
{
    public async Task<AiRunProgressSnapshot?> ReadAsync(Guid tenantId, Guid actorId, string sessionId, Guid stamp, Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (tenant.TenantId != tenantId || !configuration.Enabled || !configuration.AllowedTenantIds.Contains(tenantId)) return null;
        var actor = await identities.GetAuthenticationStateAsync(tenantId, actorId, cancellationToken);
        if (actor is null || actor.SecurityStamp != stamp ||
            await sessions.ValidateAccessAsync(tenantId, actorId, sessionId, stamp, cancellationToken) != UserAccessValidationStatus.Valid ||
            !new[] { AiCenterConstants.ChatUsePermission, AiCenterConstants.ConversationViewPermission }.All(p =>
                PermissionEvaluation.HasPermission(true, PermissionEvaluation.IsSuperAdmin(actor.Roles), actor.PermissionCodes, p))) return null;
        var run = await store.FindAsync(runId, cancellationToken);
        if (run is null || run.ActorUserId != actorId || run.TenantId != tenantId) return null;
        var progress = await queries.ToListAsync(tools.Query().Where(t => t.RunId == runId)
            .OrderBy(t => t.CreatedAt).Take(100).Select(t => new AiToolProgress(t.InvocationId, t.ToolCode, t.Status, t.CompletedAt)), cancellationToken);
        return new(run.Id, run.ConversationId, run.ProgressVersion ?? 0, run.Status, run.ErrorCode, progress);
    }
}
