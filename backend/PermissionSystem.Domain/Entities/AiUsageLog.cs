using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiUsageLog : BaseEntity
{
    public Guid RunId { get; set; }

    public Guid ProviderConfigId { get; set; }

    public int Sequence { get; set; }

    public int Round { get; set; }

    public int Attempt { get; set; } = 1;

    public AiModelRouteRole RouteRole { get; set; } = AiModelRouteRole.Primary;

    public string ModelName { get; set; } = string.Empty;

    public string? ProviderRequestId { get; set; }

    public AiInvocationStatus Status { get; set; } = AiInvocationStatus.Pending;

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    public int? TotalTokens { get; set; }

    public decimal? EstimatedCost { get; set; }

    public decimal? ReservedCost { get; set; }

    public decimal? InputTokenPricePerMillion { get; set; }

    public decimal? OutputTokenPricePerMillion { get; set; }

    public string? PricingCurrency { get; set; }

    public DateTimeOffset? ReservationExpiresAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long? DurationMilliseconds { get; set; }

    public int RetryCount { get; set; }

    public string? FinishReason { get; set; }

    public string? ErrorCode { get; set; }
}
