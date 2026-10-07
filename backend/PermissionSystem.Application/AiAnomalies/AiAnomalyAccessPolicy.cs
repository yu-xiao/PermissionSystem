using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiAnomalies;

public sealed class AiAnomalyAccessPolicy(ICurrentUserService current, ITenantContext tenant,
    IUserCredentialValidator identities, ITenantStatusChecker tenants, IAiCenterConfiguration configuration)
{
    public async Task<AuthenticatedUser> RequireAsync(string permission, bool requireEnabled, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not Guid userId || current.TenantId is not Guid tenantId ||
            userId == Guid.Empty || tenantId == Guid.Empty || tenant.TenantId != tenantId || tenant.IsSystemScopeActive)
            throw new BusinessException(ErrorCode.Unauthorized, "A current reminder identity and tenant are required.");
        var actor = await identities.GetAuthenticationStateAsync(tenantId, userId, ct);
        if (actor is null || actor.UserId != userId || actor.TenantId != tenantId ||
            actor.DepartmentId != current.DepartmentId || !await tenants.IsActiveAsync(tenantId, ct))
            throw new BusinessException(ErrorCode.Unauthorized, "Reminder identity is inactive or stale.");
        var super = PermissionEvaluation.IsSuperAdmin(actor.Roles);
        foreach (var code in new[] { permission, DemoBusinessOrderReadOnlyContract.ViewPermission })
            if (!current.HasPermission(code) || !PermissionEvaluation.HasPermission(true, super, actor.PermissionCodes, code))
                throw new BusinessException(ErrorCode.Forbidden, "Reminder operation is not authorized.");
        if (requireEnabled && (!configuration.Enabled || !configuration.EnableDemoAnomalyReminders ||
            !configuration.AllowedTenantIds.Contains(tenantId)))
            throw new BusinessException(ErrorCode.Forbidden, "Demo reminders are disabled or tenant is not approved.");
        return actor;
    }

    public async Task<AuthenticatedUser> RequireExecutionAsync(CancellationToken ct)
    {
        var actor = await RequireAsync(AiAnomalyContract.UpdatePermission, true, ct);
        await RequireAsync(AiAnomalyContract.ViewPermission, true, ct);
        await RequireAsync(AiAnomalyContract.NotificationPermission, true, ct);
        return actor;
    }
}
