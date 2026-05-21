using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class LoginLog : BaseEntity
{
    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string LoginType { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string LoginResult { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public string? TraceId { get; set; }
}
