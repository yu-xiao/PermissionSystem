namespace PermissionSystem.Application.Abstractions;

public enum UserAccessValidationStatus
{
    Valid = 0,
    InvalidSession = 1,
    InactiveUser = 2,
    StaleAuthorization = 3
}

public interface IUserSessionStatusChecker
{
    Task<UserAccessValidationStatus> ValidateAccessAsync(
        Guid tenantId,
        Guid userId,
        string sessionId,
        Guid securityStamp,
        CancellationToken cancellationToken = default);

    Task<bool> IsValidForRefreshAsync(
        Guid tenantId,
        Guid userId,
        string sessionId,
        CancellationToken cancellationToken = default);
}
