using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class NumberRule : BaseEntity
{
    public string RuleCode { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string BusinessType { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public string DateFormat { get; set; } = "yyyyMMdd";

    public int SequenceLength { get; set; } = 4;

    public NumberRuleResetCycle ResetCycle { get; set; } = NumberRuleResetCycle.Daily;

    public string Separator { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public string? Remark { get; set; }
}
