namespace PermissionSystem.Application.Abstractions;

public interface ITenantStatusChecker
{
    Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
