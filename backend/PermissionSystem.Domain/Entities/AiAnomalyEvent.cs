using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public enum AiAnomalyDeliveryStatus { Pending, CoolingDown, Disabled, RetryPending, Queued, Delivered, Suppressed, Failed }

public sealed class AiAnomalyEvent : BaseEntity
{
    public Guid RuleId { get; set; }
    public Guid RecipientUserId { get; set; }
    public long EpisodeSequence { get; set; }
    public long ObservedCount { get; set; }
    public string ScopeFingerprint { get; set; } = string.Empty;
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? CloseReason { get; set; }
    public AiAnomalyDeliveryStatus DeliveryStatus { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? ErrorCode { get; set; }
    public string DeliveryKey { get; set; } = string.Empty;
    public Guid? NotificationId { get; set; }
    public string? MessageId { get; set; }

    public bool CanAttempt(DateTimeOffset now) => ClosedAt is null && AttemptCount < 3 &&
        DeliveryStatus is not (AiAnomalyDeliveryStatus.Queued or AiAnomalyDeliveryStatus.Delivered or
            AiAnomalyDeliveryStatus.Suppressed or AiAnomalyDeliveryStatus.Failed) &&
        (NextAttemptAt is null || now >= NextAttemptAt);

    public void RecordFailure(DateTimeOffset now, string code)
    {
        ErrorCode = code;
        DeliveryStatus = AttemptCount >= 3 ? AiAnomalyDeliveryStatus.Failed : AiAnomalyDeliveryStatus.RetryPending;
        NextAttemptAt = AttemptCount >= 3 ? null : now.AddMinutes(AttemptCount == 1 ? 5 : 15);
    }

    public void Close(DateTimeOffset now, string reason)
    {
        ClosedAt ??= now;
        CloseReason ??= reason;
        if (DeliveryStatus != AiAnomalyDeliveryStatus.Delivered) DeliveryStatus = AiAnomalyDeliveryStatus.Suppressed;
        NextAttemptAt = null;
    }
}
