using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class AiAnomalyRule : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public Guid ScheduledTaskId { get; set; }
    public string RuleType { get; set; } = "DemoPendingCount";
    public int ContractVersion { get; set; } = 1;
    public bool IsEnabled { get; set; }
    public bool IsAnomalous { get; set; }
    public long EpisodeSequence { get; set; }
    public DateTimeOffset? LastNotifiedAt { get; set; }
    public Guid? ReservedEventId { get; set; }

    public bool Observe(long count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) { IsAnomalous = false; return false; }
        if (IsAnomalous) return false;
        IsAnomalous = true;
        EpisodeSequence = checked(EpisodeSequence + 1);
        return true;
    }

    public bool IsCoolingDown(DateTimeOffset now) => ReservedEventId.HasValue ||
        LastNotifiedAt.HasValue && now < LastNotifiedAt.Value.AddHours(24);
}
