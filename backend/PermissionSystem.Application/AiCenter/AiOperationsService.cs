using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiOperationsService : IAiOperationsService
{
    private static readonly HashSet<string> FeedbackReasonCodes = new(StringComparer.Ordinal)
    {
        "incorrect",
        "not_relevant",
        "unclear_source",
        "incomplete"
    };
    private readonly IRepository<AiUserFeedback> _feedbackRepository;
    private readonly IRepository<AiRun> _runRepository;
    private readonly IRepository<AiUsageLog> _usageRepository;
    private readonly IRepository<AiProviderConfig> _providerRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public AiOperationsService(
        IRepository<AiUserFeedback> feedbackRepository,
        IRepository<AiRun> runRepository,
        IRepository<AiUsageLog> usageRepository,
        IRepository<AiProviderConfig> providerRepository,
        IAsyncQueryExecutor queryExecutor,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _feedbackRepository = feedbackRepository;
        _runRepository = runRepository;
        _usageRepository = usageRepository;
        _providerRepository = providerRepository;
        _queryExecutor = queryExecutor;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AiFeedbackResponse?> GetMyFeedbackAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        await GetOwnedCompletedRunAsync(runId, userId, cancellationToken);
        var feedback = await _queryExecutor.FirstOrDefaultAsync(
            _feedbackRepository.Query().Where(entity => entity.RunId == runId && entity.UserId == userId),
            cancellationToken);
        return feedback is null ? null : ToFeedbackResponse(feedback);
    }

    public async Task<AiFeedbackResponse> SaveMyFeedbackAsync(
        Guid runId,
        SaveAiFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var run = await GetOwnedCompletedRunAsync(runId, userId, cancellationToken);
        if (!Enum.IsDefined(request.Rating))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI feedback rating is invalid.");
        }

        var reasonCode = NormalizeOptional(request.ReasonCode, 64);
        if (request.Rating == AiFeedbackRating.Negative &&
            (reasonCode is null || !FeedbackReasonCodes.Contains(reasonCode)))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "A supported reason is required for negative AI feedback.");
        }

        var feedback = await _queryExecutor.FirstOrDefaultAsync(
            _feedbackRepository.Query().Where(entity => entity.RunId == runId && entity.UserId == userId),
            cancellationToken);
        if (feedback is null)
        {
            feedback = new AiUserFeedback
            {
                TenantId = run.TenantId,
                RunId = run.Id,
                MessageId = run.ResponseMessageId!.Value,
                UserId = userId
            };
            await _feedbackRepository.AddAsync(feedback, cancellationToken);
        }
        else
        {
            _feedbackRepository.Update(feedback);
        }

        feedback.Rating = request.Rating;
        feedback.ReasonCode = request.Rating == AiFeedbackRating.Negative ? reasonCode : null;
        feedback.Comment = request.Rating == AiFeedbackRating.Negative
            ? NormalizeOptional(request.Comment, 500)
            : null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToFeedbackResponse(feedback);
    }

    public async Task<AiOperationsSummaryResponse> GetSummaryAsync(
        AiOperationsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var to = request.To ?? now;
        var from = request.From ?? to.AddDays(-30);
        if (from >= to || to - from > TimeSpan.FromDays(90) || to > now.AddMinutes(5))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI operations date range is invalid.");
        }

        var runs = await _queryExecutor.ToListAsync(
            _runRepository.Query().Where(entity => entity.CreatedAt >= from && entity.CreatedAt < to),
            cancellationToken);
        var runIds = runs.Select(entity => entity.Id).ToList();
        var usages = await _queryExecutor.ToListAsync(
            _usageRepository.Query().Where(entity => runIds.Contains(entity.RunId)),
            cancellationToken);
        var feedback = await _queryExecutor.ToListAsync(
            _feedbackRepository.Query().Where(entity => runIds.Contains(entity.RunId)),
            cancellationToken);
        var providerIds = usages.Select(entity => entity.ProviderConfigId).Distinct().ToList();
        var providerNames = (await _queryExecutor.ToListAsync(
                _providerRepository.Query().Where(entity => providerIds.Contains(entity.Id)),
                cancellationToken))
            .ToDictionary(entity => entity.Id, entity => entity.ProviderName);
        var durations = runs
            .Where(entity => entity.DurationMilliseconds.HasValue)
            .Select(entity => entity.DurationMilliseconds!.Value)
            .OrderBy(value => value)
            .ToList();

        return new AiOperationsSummaryResponse
        {
            From = from,
            To = to,
            RunCount = runs.Count,
            SuccessfulRunCount = runs.LongCount(entity => entity.Status == AiRunStatus.Completed),
            FailedRunCount = runs.LongCount(entity => entity.Status == AiRunStatus.Failed),
            FallbackRunCount = runs.LongCount(entity => entity.FallbackCount > 0),
            InputTokens = usages.Sum(entity => (long)(entity.InputTokens ?? 0)),
            OutputTokens = usages.Sum(entity => (long)(entity.OutputTokens ?? 0)),
            UnknownCostInvocationCount = usages.LongCount(entity =>
                entity.Status == AiInvocationStatus.Completed && !entity.EstimatedCost.HasValue),
            PositiveFeedbackCount = feedback.LongCount(entity => entity.Rating == AiFeedbackRating.Positive),
            NegativeFeedbackCount = feedback.LongCount(entity => entity.Rating == AiFeedbackRating.Negative),
            P95DurationMilliseconds = Percentile95(durations),
            Costs = usages
                .Where(entity => entity.EstimatedCost.HasValue && !string.IsNullOrWhiteSpace(entity.PricingCurrency))
                .GroupBy(entity => entity.PricingCurrency!)
                .Select(group => new AiCurrencyCostResponse
                {
                    Currency = group.Key,
                    Amount = group.Sum(entity => entity.EstimatedCost!.Value)
                })
                .OrderBy(entity => entity.Currency)
                .ToList(),
            Providers = usages
                .GroupBy(entity => entity.ProviderConfigId)
                .Select(group => new AiProviderOperationsResponse
                {
                    ProviderConfigId = group.Key,
                    ProviderName = providerNames.GetValueOrDefault(group.Key, "已删除 Provider"),
                    InvocationCount = group.LongCount(),
                    FailedInvocationCount = group.LongCount(entity => entity.Status == AiInvocationStatus.Failed),
                    InputTokens = group.Sum(entity => (long)(entity.InputTokens ?? 0)),
                    OutputTokens = group.Sum(entity => (long)(entity.OutputTokens ?? 0))
                })
                .OrderByDescending(entity => entity.InvocationCount)
                .ToList(),
            Daily = runs
                .GroupBy(entity => DateOnly.FromDateTime(entity.CreatedAt.UtcDateTime))
                .Select(group => new AiDailyOperationsResponse
                {
                    Date = group.Key,
                    RunCount = group.LongCount(),
                    SuccessfulRunCount = group.LongCount(entity => entity.Status == AiRunStatus.Completed),
                    PositiveFeedbackCount = feedback.LongCount(entity =>
                        group.Select(run => run.Id).Contains(entity.RunId) && entity.Rating == AiFeedbackRating.Positive),
                    NegativeFeedbackCount = feedback.LongCount(entity =>
                        group.Select(run => run.Id).Contains(entity.RunId) && entity.Rating == AiFeedbackRating.Negative)
                })
                .OrderBy(entity => entity.Date)
                .ToList()
        };
    }

    private async Task<AiRun> GetOwnedCompletedRunAsync(Guid runId, Guid userId, CancellationToken cancellationToken)
    {
        var run = await _queryExecutor.FirstOrDefaultAsync(
            _runRepository.Query().Where(entity => entity.Id == runId && entity.ActorUserId == userId),
            cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "The AI run was not found.");
        if (run.Status != AiRunStatus.Completed || !run.ResponseMessageId.HasValue)
        {
            throw new BusinessException(ErrorCode.Conflict, "Feedback can only be submitted for completed AI answers.");
        }

        return run;
    }

    private Guid RequireUserId()
    {
        return _currentUserService.UserId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "Current user identity is unavailable.");
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI feedback content is too long.");
        }

        return normalized;
    }

    private static long? Percentile95(IReadOnlyList<long> sorted)
    {
        if (sorted.Count == 0)
        {
            return null;
        }

        var index = (int)Math.Ceiling(sorted.Count * 0.95m) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    internal static AiFeedbackResponse ToFeedbackResponse(AiUserFeedback entity)
    {
        return new AiFeedbackResponse
        {
            RunId = entity.RunId,
            Rating = entity.Rating,
            ReasonCode = entity.ReasonCode,
            Comment = entity.Comment,
            UpdatedAt = entity.UpdatedAt ?? entity.CreatedAt
        };
    }
}
