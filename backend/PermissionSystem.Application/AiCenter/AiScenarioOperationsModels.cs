using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiCenter;

public static class AiScenarioOperationsContract
{
    public const int MetricsVersion = 1;
    public const int MaxRuns = 10_000;
    public const int MaxUsages = 50_000;
    public const int MaxFeedback = 10_000;
    public const int MaxPageSize = 50;
    public const string CapacityMessage = "场景统计数据超过查询上限，请缩短时间范围后重试。";
}

public sealed class AiScenarioOperationsQueryRequest
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class AiScenarioOperationsLimits
{
    public int MaxRuns => AiScenarioOperationsContract.MaxRuns;
    public int MaxUsages => AiScenarioOperationsContract.MaxUsages;
    public int MaxFeedback => AiScenarioOperationsContract.MaxFeedback;
}

public sealed class AiScenarioOperationsResponse
{
    public int MetricsVersion => AiScenarioOperationsContract.MetricsVersion;
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }
    public DateTimeOffset ObservedFrom { get; init; }
    public DateTimeOffset ObservedTo { get; init; }
    public AiScenarioOperationsLimits Limits { get; init; } = new();
    public PagedResult<AiScenarioOperationsItemResponse> Scenarios { get; init; } = new();
}

public sealed class AiScenarioOperationsItemResponse
{
    public Guid? ScenarioId { get; init; }
    public string ScenarioName { get; init; } = string.Empty;
    public string? ScenarioCode { get; init; }
    public bool ScenarioAvailable { get; init; }
    public long RunCount { get; init; }
    public long PendingRunCount { get; init; }
    public long RunningRunCount { get; init; }
    public long CompletedRunCount { get; init; }
    public long FailedRunCount { get; init; }
    public long CancelledRunCount { get; init; }
    public long UnknownStatusRunCount { get; init; }
    public long TerminalRunCount { get; init; }
    public long TimeoutFailureCount { get; init; }
    public decimal? TechnicalCompletionRate { get; init; }
    public long FeedbackEligibleRunCount { get; init; }
    public long PositiveFeedbackCount { get; init; }
    public long NegativeFeedbackCount { get; init; }
    public decimal? FeedbackCoverageRate { get; init; }
    public decimal? PositiveFeedbackRate { get; init; }
    public long DurationSampleCount { get; init; }
    public long? P95DurationMilliseconds { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long UnknownUsageInvocationCount { get; init; }
    public long UnknownCostInvocationCount { get; init; }
    public long UnsettledInvocationCount { get; init; }
    public long UnknownStatusInvocationCount { get; init; }
    public IReadOnlyList<AiCurrencyCostResponse> EstimatedCosts { get; init; } = [];
}

public interface IAiScenarioOperationsService
{
    Task<AiScenarioOperationsResponse> QueryAsync(
        AiScenarioOperationsQueryRequest request,
        CancellationToken cancellationToken = default);
}
