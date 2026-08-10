using System.Linq.Expressions;
using PermissionSystem.Domain.Common;

namespace PermissionSystem.Application.DataPermissions;

public interface IDataPermissionSpecification<TEntity>
    where TEntity : BaseEntity, IDataPermissionEntity
{
    Expression<Func<TEntity, Guid?>> UserIdSelector { get; }

    Expression<Func<TEntity, Guid?>> DepartmentIdSelector { get; }
}
