using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiBudgetPolicy : BaseEntity
{
    public string PolicyCode { get; set; } = string.Empty;

    public string PolicyName { get; set; } = string.Empty;

    public AiBudgetScopeType ScopeType { get; set; } = AiBudgetScopeType.Tenant;

    public Guid? UserId { get; set; }

    public decimal MonthlyLimit { get; set; }

    public string Currency { get; set; } = string.Empty;

    public bool IsHardLimit { get; set; } = true;

    public int AlertThresholdPercentage { get; set; } = 80;

    public bool IsEnabled { get; set; } = true;
}
