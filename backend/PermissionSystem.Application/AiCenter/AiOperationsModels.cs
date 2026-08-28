using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.AiCenter;

public sealed class SaveAiFeedbackRequest
{
    public AiFeedbackRating Rating { get; init; }

    public string? ReasonCode { get; init; }

    public string? Comment { get; init; }
}

public sealed class AiFeedbackResponse
{
    public Guid RunId { get; init; }

    public AiFeedbackRating Rating { get; init; }

    public string? ReasonCode { get; init; }

    public string? Comment { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class AiOperationsQueryRequest
{
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }
}

public sealed class AiOperationsSummaryResponse
{
    public DateTimeOffset From { get; init; }

    public DateTimeOffset To { get; init; }

    public long RunCount { get; init; }

    public long SuccessfulRunCount { get; init; }

    public long FailedRunCount { get; init; }

    public long FallbackRunCount { get; init; }

    public long InputTokens { get; init; }

    public long OutputTokens { get; init; }

    public long UnknownCostInvocationCount { get; init; }

    public long PositiveFeedbackCount { get; init; }

    public long NegativeFeedbackCount { get; init; }

    public long? P95DurationMilliseconds { get; init; }

    public IReadOnlyList<AiCurrencyCostResponse> Costs { get; init; } = [];

    public IReadOnlyList<AiProviderOperationsResponse> Providers { get; init; } = [];

    public IReadOnlyList<AiDailyOperationsResponse> Daily { get; init; } = [];
}

public sealed class AiCurrencyCostResponse
{
    public string Currency { get; init; } = string.Empty;

    public decimal Amount { get; init; }
}

public sealed class AiProviderOperationsResponse
{
    public Guid ProviderConfigId { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public long InvocationCount { get; init; }

    public long FailedInvocationCount { get; init; }

    public long InputTokens { get; init; }

    public long OutputTokens { get; init; }
}

public sealed class AiDailyOperationsResponse
{
    public DateOnly Date { get; init; }

    public long RunCount { get; init; }

    public long SuccessfulRunCount { get; init; }

    public long PositiveFeedbackCount { get; init; }

    public long NegativeFeedbackCount { get; init; }
}

public interface IAiOperationsService
{
    Task<AiFeedbackResponse?> GetMyFeedbackAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<AiFeedbackResponse> SaveMyFeedbackAsync(
        Guid runId,
        SaveAiFeedbackRequest request,
        CancellationToken cancellationToken = default);

    Task<AiOperationsSummaryResponse> GetSummaryAsync(
        AiOperationsQueryRequest request,
        CancellationToken cancellationToken = default);
}
