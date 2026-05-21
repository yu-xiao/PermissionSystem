using System.Linq.Expressions;

namespace PermissionSystem.Application.DataPermissions;

public interface IDataPermissionFilter
{
    IQueryable<TEntity> Apply<TEntity>(
        IQueryable<TEntity> query,
        DataScopeContext dataScope,
        Expression<Func<TEntity, Guid?>> userIdSelector,
        Expression<Func<TEntity, Guid?>> departmentIdSelector);
}
