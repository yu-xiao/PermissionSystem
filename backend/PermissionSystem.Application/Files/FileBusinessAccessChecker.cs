using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Files;

public sealed class FileBusinessAccessChecker : IFileBusinessAccessChecker
{
    private readonly IDataPermissionRepository<DemoBusinessOrder> _demoBusinessOrderRepository;

    public FileBusinessAccessChecker(
        IDataPermissionRepository<DemoBusinessOrder> demoBusinessOrderRepository)
    {
        _demoBusinessOrderRepository = demoBusinessOrderRepository;
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

        var query = await _demoBusinessOrderRepository.QueryVisibleAsync(cancellationToken);

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
