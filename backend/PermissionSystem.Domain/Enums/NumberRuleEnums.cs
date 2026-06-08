namespace PermissionSystem.Domain.Enums;

public enum NumberRuleResetCycle
{
    None = 0,
    Daily = 1,
    Monthly = 2,
    Yearly = 3
}

public enum NumberRuleSegmentType
{
    FixedText = 0,
    Date = 1,
    Sequence = 2,
    TenantCode = 3,
    DepartmentCode = 4,
    Custom = 5
}
