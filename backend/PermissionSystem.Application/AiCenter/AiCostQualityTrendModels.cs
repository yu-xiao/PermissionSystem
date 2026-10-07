using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiCenter;

public static class AiCostQualityTrendContract
{
    public const string Route = "/api/ai/operations/cost-quality/trends";
    public const string Grouping = "UsageCreatedAtUtcDay";
    public const int MaxDays = 91;
}

public sealed class AiCostQualityTrendQuery
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record AiCostQualityDay(DateOnly Date, DateTimeOffset BucketFrom, DateTimeOffset BucketTo,
    bool IsPartialDay, AiCostQualityPopulation Population, AiCostQualityBasis Basis,
    IReadOnlyList<AiCostQualityIssue> Issues, AiCostQualityTokenComparison InputComparison,
    AiCostQualityTokenComparison OutputLimitComparison, AiCostQualityTotalTokens TotalTokens);

public sealed record AiCostQualityCurrencyDistribution(AiCostQualityCurrencySummary Summary,
    long StoredAboveRecomputedCount, long StoredBelowRecomputedCount);

public sealed record AiCostQualityTrendResponse(Guid TenantId, DateTimeOffset From, DateTimeOffset To,
    DateTimeOffset ObservedFrom, DateTimeOffset ObservedTo, AiCostQualityPopulation Population,
    AiCostQualityBasis Basis, IReadOnlyList<AiCostQualityIssue> Issues,
    AiCostQualityTokenComparison InputComparison, AiCostQualityTokenComparison OutputLimitComparison,
    AiCostQualityTotalTokens TotalTokens, IReadOnlyList<AiCostQualityDay> Daily,
    PagedResult<AiCostQualityCurrencyDistribution> Currencies)
{
    public int MetricsVersion => 1;
    public string Scope => AiCostQualityContract.Scope;
    public string CostBasis => AiCostQualityContract.CostBasis;
    public string Grouping => AiCostQualityTrendContract.Grouping;
    public string BucketTimezone => "UTC";
    public AiCostQualityLimits Limits => new();
}
