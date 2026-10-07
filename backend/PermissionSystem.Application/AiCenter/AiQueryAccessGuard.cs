using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public interface IAiQueryAccessGuard
{
    Task<AuthenticatedUser> AuthorizeAsync(string toolCode, CancellationToken cancellationToken = default);
    Task<DataScopeContext> GetUserScopeAsync(AuthenticatedUser actor, CancellationToken cancellationToken = default);
}

public sealed class AiQueryAccessGuard(
    ICurrentUserService currentUser, ITenantContext tenant, IUserCredentialValidator identities,
    IAiCenterConfiguration configuration, IDataScopeResolver scopes) : IAiQueryAccessGuard
{
    public async Task<AuthenticatedUser> AuthorizeAsync(string toolCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || !currentUser.TenantId.HasValue)
            throw new BusinessException(ErrorCode.Unauthorized, "A valid AI query identity is required.");
        if (currentUser.TenantId != tenant.TenantId || !configuration.Enabled ||
            !configuration.AllowedTenantIds.Contains(currentUser.TenantId.Value))
            throw new BusinessException(ErrorCode.Forbidden, "AI queries are unavailable.");
        var actor = await identities.GetAuthenticationStateAsync(currentUser.TenantId.Value, currentUser.UserId.Value, cancellationToken);
        if (actor is null || actor.TenantId != tenant.TenantId || actor.UserId != currentUser.UserId ||
            actor.DepartmentId != currentUser.DepartmentId || PermissionEvaluation.IsSuperAdmin(actor.Roles) != currentUser.IsSuperAdmin)
            throw new BusinessException(ErrorCode.Unauthorized, "The AI query identity is inactive or stale.");
        var required = toolCode switch
        {
            "permission.users.search" => new[] { AiCenterConstants.ToolQueryPermission, AiCenterConstants.UserQueryPermission, "system:user:view" },
            "permission.login_logs.summary" => [AiCenterConstants.ToolQueryPermission, AiCenterConstants.LoginLogQueryPermission, "system:login-log:view"],
            "permission.operation_logs.summary" => [AiCenterConstants.ToolQueryPermission, AiCenterConstants.OperationLogQueryPermission, "system:operation-log:view"],
            "permission.reports.query_dataset" => [AiCenterConstants.ToolQueryPermission, AiCenterConstants.ReportDatasetQueryPermission, "report:view", "system:user:view"],
            "permission.diagnose" => [AiCenterConstants.ToolQueryPermission],
            DemoBusinessOrderQueryAiToolHandler.ToolCode => [AiCenterConstants.ToolQueryPermission, DemoBusinessOrderReadOnlyContract.ViewPermission],
            PermissionSystem.Application.AiKnowledge.AiKnowledgeContract.ToolCode => [AiCenterConstants.ToolQueryPermission, AiCenterConstants.KnowledgeQueryPermission],
            _ => throw new BusinessException(ErrorCode.Forbidden, "Unsupported AI query tool.")
        };
        if (!required.All(code => currentUser.HasPermission(code) &&
            PermissionEvaluation.HasPermission(true, currentUser.IsSuperAdmin, actor.PermissionCodes, code)))
            throw new BusinessException(ErrorCode.Forbidden, "The AI query is not authorized.");
        return actor;
    }

    public Task<DataScopeContext> GetUserScopeAsync(AuthenticatedUser actor, CancellationToken cancellationToken = default) =>
        scopes.ResolveAsync(new PermissionSubject(actor.TenantId, actor.UserId, actor.DepartmentId,
            PermissionEvaluation.IsSuperAdmin(actor.Roles)), cancellationToken);
}
