using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class LoginFailureRecord : BaseEntity
{
    public string UserName { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public int FailureCount { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset LastFailureAt { get; set; }
}
