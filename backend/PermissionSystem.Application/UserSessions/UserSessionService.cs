using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.UserSessions;

public sealed class UserSessionService : IUserSessionService
{
    private static readonly TimeSpan LastActiveThrottle = TimeSpan.FromSeconds(60);

    private readonly IRepository<UserSession> _sessionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;

    public UserSessionService(
        IRepository<UserSession> sessionRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreatedUserSessionResponse> CreateAsync(
        CreateUserSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid().ToString("N");
        var accessTokenId = Guid.NewGuid().ToString("N");
        var refreshTokenId = Guid.NewGuid().ToString("N");

        await _sessionRepository.AddAsync(new UserSession
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            UserName = request.UserName.Trim(),
            SessionId = sessionId,
            AccessTokenId = accessTokenId,
            RefreshTokenId = refreshTokenId,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            LoginAt = now,
            LastActiveAt = now,
            ExpiresAt = request.ExpiresAt,
            IsRevoked = false
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedUserSessionResponse
        {
            SessionId = sessionId,
            AccessTokenId = accessTokenId,
            RefreshTokenId = refreshTokenId
        };
    }

    public async Task<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var cacheValue = await _cacheService.GetAsync<bool?>(UserSessionCacheKeys.Revoked(sessionId), cancellationToken);
        if (cacheValue == true)
        {
            return true;
        }

        var session = _sessionRepository.Query().FirstOrDefault(entity => entity.SessionId == sessionId);
        return session is null || session.IsRevoked || session.ExpiresAt <= DateTimeOffset.UtcNow;
    }

    public async Task TouchAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var throttleKey = UserSessionCacheKeys.LastActiveThrottle(sessionId);
        if (await _cacheService.GetAsync<bool?>(throttleKey, cancellationToken) == true)
        {
            return;
        }

        var session = _sessionRepository.Query().FirstOrDefault(entity => entity.SessionId == sessionId);
        if (session is null || session.IsRevoked)
        {
            return;
        }

        session.LastActiveAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cacheService.SetAsync(throttleKey, true, LastActiveThrottle, cancellationToken: cancellationToken);
    }

    public async Task RevokeAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
    {
        var session = _sessionRepository.Query().FirstOrDefault(entity => entity.SessionId == sessionId);
        if (session is null)
        {
            return;
        }

        await RevokeSessionAsync(session, reason, cancellationToken);
    }

    public async Task RevokeUserSessionsAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var sessions = StageUserSessionsRevocation(userId, reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PublishRevokedSessionsAsync(sessions, cancellationToken);
    }

    public async Task RevokeTenantSessionsAsync(
        Guid tenantId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var sessions = StageSessionsRevocation(
            _sessionRepository.QueryForTenant(tenantId).Where(entity => !entity.IsRevoked),
            reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PublishRevokedSessionsAsync(sessions, cancellationToken);
    }

    public IReadOnlyCollection<RevokedUserSession> StageUserSessionsRevocation(Guid userId, string reason)
    {
        return StageSessionsRevocation(
            _sessionRepository.Query().Where(entity => entity.UserId == userId && !entity.IsRevoked),
            reason);
    }

    public async Task PublishRevokedSessionsAsync(
        IReadOnlyCollection<RevokedUserSession> sessions,
        CancellationToken cancellationToken = default)
    {
        foreach (var session in sessions)
        {
            var ttl = session.ExpiresAt - DateTimeOffset.UtcNow;
            if (ttl <= TimeSpan.Zero)
            {
                ttl = TimeSpan.FromHours(1);
            }

            await _cacheService.SetAsync(
                UserSessionCacheKeys.Revoked(session.SessionId),
                true,
                ttl,
                cancellationToken: cancellationToken);
        }
    }

    public Task<PagedResult<OnlineUserResponse>> GetOnlineUsersAsync(
        OnlineUserQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyQuery(_sessionRepository.Query(), request)
            .Where(entity => entity.ExpiresAt > DateTimeOffset.UtcNow);

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.LastActiveAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<OnlineUserResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<OnlineUserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "User session was not found.");
        EnsureVisible(entity);
        return ToResponse(entity);
    }

    public async Task KickoutAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "User session was not found.");
        EnsureVisible(entity);
        await RevokeSessionAsync(entity, string.IsNullOrWhiteSpace(reason) ? "Force logout by administrator." : reason.Trim(), cancellationToken);
    }

    private async Task RevokeSessionAsync(UserSession session, string reason, CancellationToken cancellationToken)
    {
        if (!session.IsRevoked)
        {
            session.IsRevoked = true;
            session.RevokedAt = DateTimeOffset.UtcNow;
            session.RevokedReason = reason;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var ttl = session.ExpiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            ttl = TimeSpan.FromHours(1);
        }

        await _cacheService.SetAsync(UserSessionCacheKeys.Revoked(session.SessionId), true, ttl, cancellationToken: cancellationToken);
    }

    private IReadOnlyCollection<RevokedUserSession> StageSessionsRevocation(
        IQueryable<UserSession> query,
        string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var sessions = query.ToList();
        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
            session.RevokedReason = reason;
        }

        return sessions
            .Select(session => new RevokedUserSession(session.SessionId, session.ExpiresAt))
            .ToArray();
    }

    private IQueryable<UserSession> ApplyQuery(IQueryable<UserSession> query, OnlineUserQueryRequest request)
    {
        var tenantId = ResolveTenantId(request.TenantId);
        if (tenantId.HasValue)
        {
            query = query.Where(entity => entity.TenantId == tenantId.Value);
        }

        if (!request.IsRevoked.GetValueOrDefault())
        {
            query = query.Where(entity => !entity.IsRevoked);
        }
        else if (request.IsRevoked.HasValue)
        {
            query = query.Where(entity => entity.IsRevoked == request.IsRevoked.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.UserName.Contains(keyword) ||
                entity.SessionId.Contains(keyword) ||
                (entity.IpAddress != null && entity.IpAddress.Contains(keyword)));
        }

        return query;
    }

    private Guid? ResolveTenantId(Guid? requestedTenantId)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            return requestedTenantId;
        }

        return _currentUserService.TenantId ?? requestedTenantId;
    }

    private void EnsureVisible(UserSession session)
    {
        if (!_currentUserService.IsSuperAdmin &&
            _currentUserService.TenantId.HasValue &&
            session.TenantId != _currentUserService.TenantId.Value)
        {
            throw new BusinessException(ErrorCode.NotFound, "User session was not found.");
        }
    }

    private static OnlineUserResponse ToResponse(UserSession entity)
    {
        return new OnlineUserResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UserId = entity.UserId,
            UserName = entity.UserName,
            SessionId = entity.SessionId,
            IpAddress = entity.IpAddress,
            UserAgent = entity.UserAgent,
            LoginAt = entity.LoginAt,
            LastActiveAt = entity.LastActiveAt,
            ExpiresAt = entity.ExpiresAt,
            IsRevoked = entity.IsRevoked,
            RevokedAt = entity.RevokedAt,
            RevokedReason = entity.RevokedReason
        };
    }
}
