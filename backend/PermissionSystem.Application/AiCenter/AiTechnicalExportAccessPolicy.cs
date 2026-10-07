using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiTechnicalExportAccessPolicy(
    ICurrentUserService current, ITenantContext tenant, IUserCredentialValidator identities)
{
    public async Task<AiTechnicalExportAccess> AuthorizeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!current.IsAuthenticated || current.UserId is not Guid actorId || actorId == Guid.Empty ||
            current.TenantId is not Guid identityTenant || identityTenant == Guid.Empty)
            throw new BusinessException(ErrorCode.Unauthorized, "技术元数据导出需要有效身份。");
        if (!tenant.IsResolved || tenant.TenantId is not Guid target || target == Guid.Empty || tenant.IsSystemScopeActive)
            throw new BusinessException(ErrorCode.Forbidden, "技术元数据导出需要明确的目标租户。");
        var actor = await identities.GetAuthenticationStateAsync(identityTenant, actorId, ct);
        if (actor is null || actor.UserId != actorId || actor.TenantId != identityTenant ||
            (current.SecurityStamp.HasValue && current.SecurityStamp != actor.SecurityStamp))
            throw new BusinessException(ErrorCode.Unauthorized, "导出身份已失效或过期。");
        var super = PermissionEvaluation.IsSuperAdmin(actor.Roles);
        if (super != current.IsSuperAdmin || (!super && identityTenant != target) ||
            (super && tenant.Source is not ("Header" or "Request")))
            throw new BusinessException(ErrorCode.Forbidden, "导出目标租户未获授权。");
        foreach (var code in new[] { AiCenterConstants.OperationsViewPermission, AiCenterConstants.OperationsExportPermission })
            if (!current.HasPermission(code) || !PermissionEvaluation.HasPermission(true, super, actor.PermissionCodes, code))
                throw new BusinessException(ErrorCode.Forbidden, "当前没有技术元数据导出权限。");
        if (await identities.ResolveActiveTenantIdAsync(target.ToString("D"), ct) != target)
            throw new BusinessException(ErrorCode.Forbidden, "导出目标租户已停用。");
        return new(actorId, identityTenant, target, actor.SecurityStamp);
    }

    public async Task ReauthorizeAsync(AiTechnicalExportAccess access, CancellationToken ct)
    {
        if (await AuthorizeAsync(ct) != access)
            throw new BusinessException(ErrorCode.Forbidden, "导出期间身份或目标租户发生变化。");
    }
}

public sealed record AiTechnicalExportAccess(Guid ActorId, Guid IdentityTenantId, Guid TargetTenantId, Guid SecurityStamp);
