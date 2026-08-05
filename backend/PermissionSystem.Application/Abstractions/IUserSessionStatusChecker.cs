namespace PermissionSystem.Application.Abstractions;

public interface IUserSessionStatusChecker
{
    Task<bool> IsValidForRefreshAsync(
        Guid tenantId,
        Guid userId,
        string sessionId,
        CancellationToken cancellationToken = default);
}
