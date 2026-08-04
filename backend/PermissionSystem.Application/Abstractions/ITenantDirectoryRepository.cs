using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Application.Abstractions;

public interface ITenantDirectoryRepository
{
    IQueryable<Tenant> Query();

    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
