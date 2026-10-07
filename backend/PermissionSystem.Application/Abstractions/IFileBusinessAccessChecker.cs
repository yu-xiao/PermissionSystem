using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Application.Abstractions;

public interface IFileBusinessAccessChecker
{
    Task<bool> CanReadAsync(FileResource file, CancellationToken cancellationToken = default) =>
        CanAccessAsync(file.BusinessType, file.BusinessId, cancellationToken);

    Task<bool> CanUploadAsync(string? businessType, Guid? businessId, CancellationToken cancellationToken = default) =>
        CanAccessAsync(businessType, businessId, cancellationToken);

    Task EnsureDeleteAccessAsync(FileResource file, CancellationToken cancellationToken = default) =>
        EnsureAccessAsync(file, cancellationToken);
    Task<bool> CanAccessAsync(
        string? businessType,
        Guid? businessId,
        CancellationToken cancellationToken = default);

    Task EnsureAccessAsync(
        FileResource fileResource,
        CancellationToken cancellationToken = default);
}
