using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class SensitiveOperationVerification : BaseEntity
{
    public Guid UserId { get; set; }

    public string SessionId { get; set; } = string.Empty;

    public string OperationCode { get; set; } = string.Empty;

    public string VerificationMethod { get; set; } = "Password";

    public DateTimeOffset ExpiresAt { get; set; }

    public int FailedAttemptCount { get; set; }

    public DateTimeOffset? LockedAt { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public string? TicketHash { get; set; }

    public DateTimeOffset? TicketExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

}
