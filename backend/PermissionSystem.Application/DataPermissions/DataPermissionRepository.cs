using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Repositories;

namespace PermissionSystem.Application.DataPermissions;

public sealed class DataPermissionRepository<TEntity> : IDataPermissionRepository<TEntity>
    where TEntity : BaseEntity, IDataPermissionEntity
{
    private readonly IRepository<TEntity> _repository;
    private readonly IDataScopeService _dataScopeService;
    private readonly IDataPermissionFilter _dataPermissionFilter;
    private readonly IDataPermissionSpecification<TEntity> _specification;
    private readonly HashSet<Guid> _visibleEntityIds = [];

    public DataPermissionRepository(
        IRepository<TEntity> repository,
        IDataScopeService dataScopeService,
        IDataPermissionFilter dataPermissionFilter,
        IDataPermissionSpecification<TEntity> specification)
    {
        _repository = repository;
        _dataScopeService = dataScopeService;
        _dataPermissionFilter = dataPermissionFilter;
        _specification = specification;
    }

    public async Task<IQueryable<TEntity>> QueryVisibleAsync(CancellationToken cancellationToken = default)
    {
        var dataScope = await _dataScopeService.GetCurrentUserDataScopeAsync(cancellationToken);
        return _repository.Query().ApplyDataPermission(
            _dataPermissionFilter,
            dataScope,
            _specification.UserIdSelector,
            _specification.DepartmentIdSelector);
    }

    public async Task<TEntity?> GetVisibleByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = await QueryVisibleAsync(cancellationToken);
        var entity = query.FirstOrDefault(entity => entity.Id == id);
        if (entity is not null)
        {
            _visibleEntityIds.Add(entity.Id);
        }

        return entity;
    }

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return _repository.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        EnsureVisibleForWrite(entity);
        _repository.Update(entity);
    }

    public void Remove(TEntity entity)
    {
        EnsureVisibleForWrite(entity);
        _repository.Remove(entity);
    }

    private void EnsureVisibleForWrite(TEntity entity)
    {
        if (!_visibleEntityIds.Contains(entity.Id))
        {
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name} must be loaded through GetVisibleByIdAsync before it can be modified.");
        }
    }
}
