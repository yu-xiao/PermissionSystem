using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class UserSession : BaseEntity
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;

    public string? AccessTokenId { get; set; }

    public string? RefreshTokenId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset LoginAt { get; set; }

    public DateTimeOffset LastActiveAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }
}
