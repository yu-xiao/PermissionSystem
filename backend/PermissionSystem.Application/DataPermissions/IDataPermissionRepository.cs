using PermissionSystem.Domain.Common;

namespace PermissionSystem.Application.DataPermissions;

public interface IDataPermissionRepository<TEntity>
    where TEntity : BaseEntity, IDataPermissionEntity
{
    Task<IQueryable<TEntity>> QueryVisibleAsync(CancellationToken cancellationToken = default);

    Task<TEntity?> GetVisibleByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);
}
