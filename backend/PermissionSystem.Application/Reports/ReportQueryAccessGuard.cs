using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Reports;

public interface IReportQueryAccessGuard
{
    Task<ReportExecutionContext> AuthorizeAsync(ReportDatasetDefinition dataset, bool export,
        CancellationToken cancellationToken = default);
}

public sealed class ReportQueryAccessGuard(ICurrentUserService current, ITenantContext tenant,
    IUserCredentialValidator identities, IDataScopeResolver scopes) : IReportQueryAccessGuard
{
    public async Task<ReportExecutionContext> AuthorizeAsync(ReportDatasetDefinition dataset, bool export,
        CancellationToken cancellationToken = default)
    {
        if (!current.IsAuthenticated || current.UserId is not Guid userId || current.TenantId is not Guid tenantId ||
            tenantId == Guid.Empty || userId == Guid.Empty || tenantId != tenant.TenantId)
            throw new BusinessException(ErrorCode.Unauthorized, "A valid report identity and tenant are required.");
        var actor = await identities.GetAuthenticationStateAsync(tenantId, userId, cancellationToken);
        if (actor is null || actor.TenantId != tenantId || actor.UserId != userId || actor.DepartmentId != current.DepartmentId ||
            PermissionEvaluation.IsSuperAdmin(actor.Roles) != current.IsSuperAdmin)
            throw new BusinessException(ErrorCode.Unauthorized, "The report identity is inactive or stale.");
        var required = new List<string> { "report:view" };
        if (export) required.Add("report:export");
        if (dataset.Capability == ReportDatasetCapabilities.UserDirectory) required.Add("system:user:view");
        if (!required.All(code => current.HasPermission(code) &&
            PermissionEvaluation.HasPermission(true, current.IsSuperAdmin, actor.PermissionCodes, code)))
            throw new BusinessException(ErrorCode.Forbidden, "The report query is not authorized.");
        var scope = await scopes.ResolveAsync(new(tenantId, userId, actor.DepartmentId, current.IsSuperAdmin), cancellationToken);
        if (scope.CurrentUserId != userId || scope.CurrentDepartmentId != actor.DepartmentId)
            throw new BusinessException(ErrorCode.Forbidden, "The resolved report scope does not match the current identity.");
        if (scope.DepartmentIds.Count > ReportDatasetCapabilities.MaxDepartmentIds)
            throw new BusinessException(ErrorCode.ValidationFailed, "The report department scope exceeds its supported limit.");
        if (dataset.Capability != ReportDatasetCapabilities.UserDirectory && !scope.HasAllDataScope)
            throw new BusinessException(ErrorCode.Forbidden, "The report dataset does not support the current data scope.");
        return new() { TenantId = tenantId, ActorUserId = userId, Scope = scope };
    }
}
