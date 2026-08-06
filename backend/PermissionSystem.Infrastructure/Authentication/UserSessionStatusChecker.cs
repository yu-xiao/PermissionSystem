using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Authentication;

public sealed class UserSessionStatusChecker : IUserSessionStatusChecker
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;

    public UserSessionStatusChecker(AppDbContext dbContext, ICacheService cacheService)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
    }

    public async Task<UserAccessValidationStatus> ValidateAccessAsync(
        Guid tenantId,
        Guid userId,
        string sessionId,
        Guid securityStamp,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty ||
            userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(sessionId) ||
            securityStamp == Guid.Empty)
        {
            return UserAccessValidationStatus.InvalidSession;
        }

        var cacheValue = await _cacheService.GetAsync<bool?>(
            UserSessionCacheKeys.Revoked(sessionId),
            cancellationToken);
        if (cacheValue == true)
        {
            return UserAccessValidationStatus.InvalidSession;
        }

        var state = await (
                from session in _dbContext.UserSessions.IgnoreQueryFilters().AsNoTracking()
                where session.TenantId == tenantId &&
                    session.UserId == userId &&
                    session.SessionId == sessionId
                join user in _dbContext.Users.IgnoreQueryFilters().AsNoTracking()
                    on new { session.TenantId, session.UserId }
                    equals new { user.TenantId, UserId = user.Id }
                    into userMatches
                from user in userMatches.DefaultIfEmpty()
                select new
                {
                    SessionIsDeleted = session.IsDeleted,
                    session.IsRevoked,
                    session.ExpiresAt,
                    UserIsDeleted = user == null || user.IsDeleted,
                    UserIsEnabled = user != null && user.IsEnabled,
                    SecurityStamp = user == null ? Guid.Empty : user.SecurityStamp
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (state is null ||
            state.SessionIsDeleted ||
            state.IsRevoked ||
            state.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return UserAccessValidationStatus.InvalidSession;
        }

        if (state.UserIsDeleted || !state.UserIsEnabled)
        {
            return UserAccessValidationStatus.InactiveUser;
        }

        return state.SecurityStamp == securityStamp
            ? UserAccessValidationStatus.Valid
            : UserAccessValidationStatus.StaleAuthorization;
    }

    public async Task<bool> IsValidForRefreshAsync(
        Guid tenantId,
        Guid userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var cacheValue = await _cacheService.GetAsync<bool?>(
            UserSessionCacheKeys.Revoked(sessionId),
            cancellationToken);
        if (cacheValue == true)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        return await _dbContext.UserSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(entity =>
                !entity.IsDeleted &&
                entity.TenantId == tenantId &&
                entity.UserId == userId &&
                entity.SessionId == sessionId &&
                !entity.IsRevoked &&
                entity.ExpiresAt > now,
                cancellationToken);
    }
}
