using System.Text.Json.Serialization;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiCenter;

public static class AiCostQualityContract
{
    public const string Route = "/api/ai/operations/cost-quality";
    public const string Scope = "CurrentTenantReadableRunInvocations";
    public const string CostBasis = "HistoricalSnapshotEstimateNotSupplierInvoice";
    public const int MaxUsages = 50_000;
    public const int ReadSeconds = 10;
    public const string CapacityMessage = "估算质量数据超过读取上限，请缩短调用记录时间范围。";
}

public sealed class AiCostQualityQuery
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record AiCostQualityBasis(long RecordedBothCount, long MixedFallbackCount,
    long EstimatedBothCount, long UnusableTokenPairCount);
public sealed record AiCostQualityPopulation(long InvocationCount, long TerminalCount, long UnsettledCount,
    long UnknownStatusCount, long ComparableCostCount, long ConsistentCostCount, long DifferentCostCount, long UncomparableCostCount);
public sealed record AiCostQualityIssue(string Code, long Count);
public sealed record AiCostQualityTokenComparison(long SampleCount, long RecordedTokens, long EstimatedTokens,
    long DifferenceTokens, long AbsoluteDifferenceTokens, decimal? WeightedRatioPercentage,
    long ZeroEstimatePairCount, long AboveEstimateCount);
public sealed record AiCostQualityTotalTokens(long ComparableCount, long DifferentCount);
public sealed record AiCostQualityCurrencySummary(string Currency, long TerminalCount, AiCostQualityBasis Basis,
    long ComparableCostCount, long ConsistentCostCount, long DifferentCostCount, long UncomparableCostCount,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal? StoredCost,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal? RecomputedCost,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal? DifferenceCost,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal? AbsoluteDifferenceCost);
public sealed record AiCostQualityLimits
{
    public int MaxUsages => AiCostQualityContract.MaxUsages;
    public int ReadSeconds => AiCostQualityContract.ReadSeconds;
}
public sealed record AiCostQualityResponse(Guid TenantId, DateTimeOffset From, DateTimeOffset To,
    DateTimeOffset ObservedFrom, DateTimeOffset ObservedTo, AiCostQualityPopulation Population,
    AiCostQualityBasis Basis, IReadOnlyList<AiCostQualityIssue> Issues,
    AiCostQualityTokenComparison InputComparison, AiCostQualityTokenComparison OutputLimitComparison,
    AiCostQualityTotalTokens TotalTokens, PagedResult<AiCostQualityCurrencySummary> Currencies)
{
    public int MetricsVersion => 1;
    public string Scope => AiCostQualityContract.Scope;
    public string CostBasis => AiCostQualityContract.CostBasis;
    public AiCostQualityLimits Limits => new();
}

public interface IAiCostQualityService
{
    Task<AiCostQualityResponse> QueryAsync(AiCostQualityQuery request, CancellationToken ct = default);
    Task<AiCostQualityTrendResponse> QueryTrendAsync(AiCostQualityTrendQuery request, CancellationToken ct = default);
}
