using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.DemoBusinessOrders;

public sealed class DemoBusinessOrderValidator : IDemoBusinessOrderValidator
{
    private readonly IRepository<Department> _departmentRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;

    public DemoBusinessOrderValidator(
        IRepository<Department> departmentRepository,
        IAsyncQueryExecutor queryExecutor)
    {
        _departmentRepository = departmentRepository;
        _queryExecutor = queryExecutor;
    }

    public async Task EnsureDepartmentAvailableAsync(
        Guid? departmentId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!departmentId.HasValue)
        {
            return;
        }

        var exists = await _queryExecutor.AnyAsync(
            _departmentRepository.Query().Where(entity =>
                entity.Id == departmentId.Value &&
                entity.TenantId == tenantId &&
                entity.IsEnabled),
            cancellationToken);
        if (!exists)
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "The selected department is unavailable in the current tenant.");
        }
    }
}
