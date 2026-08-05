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
