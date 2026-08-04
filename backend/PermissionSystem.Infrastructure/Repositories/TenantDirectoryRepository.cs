using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Infrastructure.Repositories;

public sealed class TenantDirectoryRepository : ITenantDirectoryRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public TenantDirectoryRepository(
        AppDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public IQueryable<Tenant> Query()
    {
        EnsureSuperAdministrator();
        return _dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(entity => !entity.IsDeleted);
    }

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdministrator();
        return _dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(entity => !entity.IsDeleted)
            .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
    }

    private void EnsureSuperAdministrator()
    {
        if (!_currentUserService.IsSuperAdmin)
        {
            throw new BusinessException(
                ErrorCode.Forbidden,
                "Only super administrators can access the cross-tenant directory.");
        }
    }
}
