using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.UserSessions;

public static class UserSessionCacheKeys
{
    public static string Revoked(string sessionId)
    {
        return $"ps:session:revoked:{sessionId}";
    }

    public static string LastActiveThrottle(string sessionId)
    {
        return $"ps:session:last-active:{sessionId}";
    }
}

public sealed class CreateUserSessionRequest
{
    public Guid TenantId { get; init; }

    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }
}

public sealed class CreatedUserSessionResponse
{
    public string SessionId { get; init; } = string.Empty;

    public string AccessTokenId { get; init; } = string.Empty;

    public string RefreshTokenId { get; init; } = string.Empty;
}

public sealed class OnlineUserQueryRequest : PaginationRequest
{
    public Guid? TenantId { get; init; }

    public string? Keyword { get; init; }

    public bool? IsRevoked { get; init; }
}

public sealed class OnlineUserResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public DateTimeOffset LoginAt { get; init; }

    public DateTimeOffset LastActiveAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public bool IsRevoked { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public string? RevokedReason { get; init; }
}

public sealed class KickoutUserSessionRequest
{
    public string? Reason { get; init; }
}

public interface IUserSessionService
{
    Task<CreatedUserSessionResponse> CreateAsync(CreateUserSessionRequest request, CancellationToken cancellationToken = default);

    Task<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken = default);

    Task TouchAsync(string sessionId, CancellationToken cancellationToken = default);

    Task RevokeAsync(string sessionId, string reason, CancellationToken cancellationToken = default);

    Task<PagedResult<OnlineUserResponse>> GetOnlineUsersAsync(OnlineUserQueryRequest request, CancellationToken cancellationToken = default);

    Task<OnlineUserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task KickoutAsync(Guid id, string? reason, CancellationToken cancellationToken = default);
}
