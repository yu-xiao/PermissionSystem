using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Files;

public sealed class FileBusinessAccessChecker : IFileBusinessAccessChecker
{
    private readonly IDataPermissionRepository<DemoBusinessOrder> _demoBusinessOrderRepository;
    private readonly AiKnowledgeAccessPolicy? _knowledge;

    public FileBusinessAccessChecker(
        IDataPermissionRepository<DemoBusinessOrder> demoBusinessOrderRepository,
        AiKnowledgeAccessPolicy? knowledge = null)
    {
        _demoBusinessOrderRepository = demoBusinessOrderRepository;
        _knowledge = knowledge;
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

        if (string.Equals(businessType.Trim(), AiKnowledgeContract.BusinessType, StringComparison.OrdinalIgnoreCase))
            return _knowledge is not null && await _knowledge.CanReadDocumentAsync(businessId.Value, cancellationToken);

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
        if (!await CanReadAsync(fileResource, cancellationToken))
        {
            throw new BusinessException(
                ErrorCode.NotFound,
                "File was not found.");
        }
    }

    public Task<bool> CanReadAsync(FileResource file, CancellationToken cancellationToken = default) =>
        string.Equals(file.BusinessType, AiKnowledgeContract.BusinessType, StringComparison.OrdinalIgnoreCase)
            ? _knowledge?.CanReadFileAsync(file, cancellationToken) ?? Task.FromResult(false)
            : CanAccessAsync(file.BusinessType, file.BusinessId, cancellationToken);

    public Task<bool> CanUploadAsync(string? businessType, Guid? businessId, CancellationToken cancellationToken = default) =>
        string.Equals(businessType?.Trim(), AiKnowledgeContract.BusinessType, StringComparison.OrdinalIgnoreCase)
            ? businessId.HasValue && _knowledge is not null ? _knowledge.CanManageAsync(businessId.Value, cancellationToken) : Task.FromResult(false)
            : CanAccessAsync(businessType, businessId, cancellationToken);

    public Task EnsureDeleteAccessAsync(FileResource file, CancellationToken cancellationToken = default)
    {
        if (string.Equals(file.BusinessType, AiKnowledgeContract.BusinessType, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException(ErrorCode.Forbidden, "Delete knowledge files through the knowledge document service.");
        return EnsureAccessAsync(file, cancellationToken);
    }
}
