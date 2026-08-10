using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Files;

public sealed class FileBusinessAccessChecker : IFileBusinessAccessChecker
{
    private readonly IRepository<DemoBusinessOrder> _demoBusinessOrderRepository;
    private readonly IDataScopeService _dataScopeService;
    private readonly IDataPermissionFilter _dataPermissionFilter;

    public FileBusinessAccessChecker(
        IRepository<DemoBusinessOrder> demoBusinessOrderRepository,
        IDataScopeService dataScopeService,
        IDataPermissionFilter dataPermissionFilter)
    {
        _demoBusinessOrderRepository = demoBusinessOrderRepository;
        _dataScopeService = dataScopeService;
        _dataPermissionFilter = dataPermissionFilter;
    }

    public async Task<bool> CanAccessAsync(
        string? businessType,
        Guid? businessId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(businessType) && !businessId.HasValue)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(businessType) ||
            !businessId.HasValue ||
            businessId.Value == Guid.Empty)
        {
            return false;
        }

        if (!string.Equals(
                businessType.Trim(),
                DemoBusinessOrderConstants.BusinessType,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dataScope = await _dataScopeService.GetCurrentUserDataScopeAsync(cancellationToken);
        var query = _demoBusinessOrderRepository.Query().ApplyDataPermission(
            _dataPermissionFilter,
            dataScope,
            entity => entity.CreatedBy,
            entity => entity.DepartmentId);

        return query.Any(entity => entity.Id == businessId.Value);
    }

    public async Task EnsureAccessAsync(
        FileResource fileResource,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAsync(fileResource.BusinessType, fileResource.BusinessId, cancellationToken))
        {
            throw new BusinessException(
                ErrorCode.NotFound,
                "File was not found.");
        }
    }
}
