using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class NumberRuleSegment : BaseEntity
{
    public Guid RuleId { get; set; }

    public NumberRuleSegmentType SegmentType { get; set; }

    public string SegmentValue { get; set; } = string.Empty;

    public int Sort { get; set; }
}
