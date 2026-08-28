using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiBudgetPolicyResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string PolicyCode { get; init; } = string.Empty;

    public string PolicyName { get; init; } = string.Empty;

    public AiBudgetScopeType ScopeType { get; init; }

    public Guid? UserId { get; init; }

    public decimal MonthlyLimit { get; init; }

    public string Currency { get; init; } = string.Empty;

    public bool IsHardLimit { get; init; }

    public int AlertThresholdPercentage { get; init; }

    public bool IsEnabled { get; init; }

    public decimal CurrentAmount { get; init; }

    public bool IsAlertThresholdExceeded { get; init; }

    public bool IsLimitExceeded { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class SaveAiBudgetPolicyRequest
{
    public Guid? TenantId { get; init; }

    public string PolicyCode { get; init; } = string.Empty;

    public string PolicyName { get; init; } = string.Empty;

    public AiBudgetScopeType ScopeType { get; init; } = AiBudgetScopeType.Tenant;

    public Guid? UserId { get; init; }

    public decimal MonthlyLimit { get; init; }

    public string Currency { get; init; } = string.Empty;

    public bool IsHardLimit { get; init; } = true;

    public int AlertThresholdPercentage { get; init; } = 80;

    public bool IsEnabled { get; init; } = true;

    public byte[]? ConcurrencyToken { get; init; }
}

public interface IAiBudgetService
{
    Task<IReadOnlyList<AiBudgetPolicyResponse>> GetPoliciesAsync(CancellationToken cancellationToken = default);

    Task<AiBudgetPolicyResponse> SavePolicyAsync(
        SaveAiBudgetPolicyRequest request,
        CancellationToken cancellationToken = default);

    Task ReserveInvocationAsync(
        AiUsageLog usage,
        AiProviderConfig provider,
        Guid userId,
        int estimatedInputTokens,
        int maxOutputTokens,
        CancellationToken cancellationToken = default);

    Task SettleInvocationAsync(
        AiUsageLog usage,
        CancellationToken cancellationToken = default);
}
