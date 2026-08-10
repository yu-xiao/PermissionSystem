using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Application.Abstractions;

public interface IFileBusinessAccessChecker
{
    Task<bool> CanAccessAsync(
        string? businessType,
        Guid? businessId,
        CancellationToken cancellationToken = default);

    Task EnsureAccessAsync(
        FileResource fileResource,
        CancellationToken cancellationToken = default);
}
