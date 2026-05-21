using System.Linq.Expressions;

namespace PermissionSystem.Application.DataPermissions;

public static class DataPermissionQueryableExtensions
{
    /// <summary>
    /// Example:
    /// orders.ApplyDataPermission(filter, dataScope, order => order.CreatedBy, order => order.DepartmentId)
    /// </summary>
    public static IQueryable<TEntity> ApplyDataPermission<TEntity>(
        this IQueryable<TEntity> query,
        IDataPermissionFilter filter,
        DataScopeContext dataScope,
        Expression<Func<TEntity, Guid?>> userIdSelector,
        Expression<Func<TEntity, Guid?>> departmentIdSelector)
    {
        return filter.Apply(query, dataScope, userIdSelector, departmentIdSelector);
    }
}
