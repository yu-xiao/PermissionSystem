using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class SensitiveOperationVerification : BaseEntity
{
    public Guid UserId { get; set; }

    public string OperationCode { get; set; } = string.Empty;

    public string VerifyCode { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}
