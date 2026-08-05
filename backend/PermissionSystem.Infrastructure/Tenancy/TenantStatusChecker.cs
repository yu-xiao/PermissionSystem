using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Tenancy;

public sealed class TenantStatusChecker : ITenantStatusChecker
{
    private readonly AppDbContext _dbContext;

    public TenantStatusChecker(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(entity => !entity.IsDeleted && entity.Id == tenantId && entity.Status == TenantStatus.Active, cancellationToken);
    }
}
