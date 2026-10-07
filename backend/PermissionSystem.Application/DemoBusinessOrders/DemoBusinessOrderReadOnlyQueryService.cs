using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.DemoBusinessOrders;

public sealed class DemoBusinessOrderReadOnlyQueryService(
    IDataPermissionRepository<DemoBusinessOrder> orders, IRepository<Department> departments,
    ICurrentUserService current, ITenantContext tenant, IUserCredentialValidator identities,
    IDataScopeResolver scopes, IDataPermissionFilter filter, IDataPermissionSpecification<DemoBusinessOrder> specification,
    IAsyncQueryExecutor queries) : IDemoBusinessOrderReadOnlyQueryService
{
    public async Task<DemoBusinessOrderReadOnlyResult> QueryAsync(DemoBusinessOrderReadOnlyQuery query,
        CancellationToken cancellationToken = default)
    {
        var effective = DemoBusinessOrderReadOnlyContract.Normalize(query);
        var (actor, scope) = await AuthorizeAsync(cancellationToken);
        var source = await BuildQueryAsync(actor, scope, effective, cancellationToken);
        var queriedAt = DateTimeOffset.UtcNow;
        var total = await queries.LongCountAsync(source, cancellationToken);
        var rows = await ReadRowsAsync(source.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            .Take(effective.Limit!.Value), cancellationToken);
        var (latestActor, latestScope) = await AuthorizeAsync(cancellationToken);
        if (actor.UserId != latestActor.UserId || actor.TenantId != latestActor.TenantId || !SameScope(scope, latestScope))
            throw new BusinessException(ErrorCode.Forbidden, "Demo query access changed; please retry.");
        return new(effective, new() { TotalCount = total, Items = rows }, scope, queriedAt);
    }

    public async Task<bool> CanReadAsync(DemoBusinessOrderReadOnlyQuery query, DemoBusinessOrderTableData data,
        CancellationToken cancellationToken = default)
    {
        var effective = DemoBusinessOrderReadOnlyContract.Normalize(query);
        if (data.Items is null || data.TotalCount < 0 || data.Items.Count > effective.Limit ||
            data.Items.Any(item => item is null || item.Id == Guid.Empty) ||
            data.Items.Select(item => item.Id).Distinct().Count() != data.Items.Count) return false;
        var (actor, scope) = await AuthorizeAsync(cancellationToken);
        var source = await BuildQueryAsync(actor, scope, effective, cancellationToken);
        var ids = data.Items.Select(item => item.Id).ToArray();
        var actual = await ReadRowsAsync(source.Where(item => ids.Contains(item.Id)), cancellationToken);
        var expected = data.Items.ToDictionary(item => item.Id);
        var valid = actual.Count == ids.Length && actual.All(item => expected[item.Id] == item);
        var (latestActor, latestScope) = await AuthorizeAsync(cancellationToken);
        return valid && latestActor.UserId == actor.UserId && latestActor.TenantId == actor.TenantId && SameScope(scope, latestScope);
    }

    private async Task<(AuthenticatedUser Actor, DataScopeContext Scope)> AuthorizeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!current.IsAuthenticated || current.UserId is not Guid userId || current.TenantId is not Guid tenantId ||
            userId == Guid.Empty || tenantId == Guid.Empty || tenantId != tenant.TenantId)
            throw new BusinessException(ErrorCode.Unauthorized, "A current Demo query identity and tenant are required.");
        var actor = await identities.GetAuthenticationStateAsync(tenantId, userId, cancellationToken);
        if (actor is null || actor.TenantId != tenantId || actor.UserId != userId ||
            actor.DepartmentId != current.DepartmentId || PermissionEvaluation.IsSuperAdmin(actor.Roles) != current.IsSuperAdmin)
            throw new BusinessException(ErrorCode.Unauthorized, "The Demo query identity is inactive or stale.");
        if (!current.HasPermission(DemoBusinessOrderReadOnlyContract.ViewPermission) ||
            !PermissionEvaluation.HasPermission(true, current.IsSuperAdmin, actor.PermissionCodes, DemoBusinessOrderReadOnlyContract.ViewPermission))
            throw new BusinessException(ErrorCode.Forbidden, "Demo query is not authorized.");
        var scope = await scopes.ResolveAsync(new(tenantId, userId, actor.DepartmentId, current.IsSuperAdmin), cancellationToken);
        if (scope.CurrentUserId != userId || scope.CurrentDepartmentId != actor.DepartmentId)
            throw new BusinessException(ErrorCode.Forbidden, "Demo scope does not match the current identity.");
        return (actor, scope);
    }

    private async Task<IQueryable<DemoBusinessOrder>> BuildQueryAsync(AuthenticatedUser actor, DataScopeContext scope,
        DemoBusinessOrderReadOnlyQuery query, CancellationToken cancellationToken)
    {
        var source = (await orders.QueryVisibleAsync(cancellationToken)).Where(item => item.TenantId == actor.TenantId && !item.IsDeleted)
            .ApplyDataPermission(filter, scope, specification.UserIdSelector, specification.DepartmentIdSelector);
        if (query.DepartmentScope == "CurrentDepartment")
        {
            if (actor.DepartmentId is not Guid departmentId || !await queries.AnyAsync(departments.Query().Where(item =>
                item.Id == departmentId && item.TenantId == actor.TenantId && !item.IsDeleted && item.IsEnabled), cancellationToken))
                throw new BusinessException(ErrorCode.ValidationFailed, "当前部门不可用，请明确查询范围。");
            source = source.Where(item => item.DepartmentId == departmentId);
        }
        if (query.Keyword is string keyword)
            source = source.Where(item => item.OrderNo.Contains(keyword) || item.Title.Contains(keyword));
        if (query.ApprovalStatus is string statusText)
        {
            var status = Enum.Parse<ApprovalStatus>(statusText);
            source = source.Where(item => item.ApprovalStatus == status);
        }
        if (query.DepartmentId is Guid explicitDepartment) source = source.Where(item => item.DepartmentId == explicitDepartment);
        return source;
    }

    private async Task<List<DemoBusinessOrderReadOnlyRow>> ReadRowsAsync(IQueryable<DemoBusinessOrder> source,
        CancellationToken cancellationToken)
    {
        var rows = await queries.ToListAsync(source.Select(item => new Row(item.Id, item.OrderNo, item.Title,
            item.ApprovalStatus, item.DepartmentId, item.CreatedAt)), cancellationToken);
        return rows.Select(item => new DemoBusinessOrderReadOnlyRow(item.Id, item.OrderNo, item.Title,
            item.ApprovalStatus.ToString(), item.DepartmentId, item.CreatedAt)).ToList();
    }

    private static bool SameScope(DataScopeContext left, DataScopeContext right) =>
        left.ScopeType == right.ScopeType && left.CurrentUserId == right.CurrentUserId &&
        left.CurrentDepartmentId == right.CurrentDepartmentId && left.IncludesCurrentUser == right.IncludesCurrentUser &&
        left.DepartmentIds.ToHashSet().SetEquals(right.DepartmentIds);

    private sealed record Row(Guid Id, string OrderNo, string Title, ApprovalStatus ApprovalStatus,
        Guid? DepartmentId, DateTimeOffset CreatedAt);
}
