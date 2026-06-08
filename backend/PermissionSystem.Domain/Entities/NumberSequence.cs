using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class NumberSequence : BaseEntity
{
    public string RuleCode { get; set; } = string.Empty;

    public string SequenceKey { get; set; } = string.Empty;

    public long CurrentValue { get; set; }

    public DateTimeOffset? LastGeneratedAt { get; set; }
}
