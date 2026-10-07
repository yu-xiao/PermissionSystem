using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiScenarioOperationsService(
    IRepository<AiRun> runs,
    IRepository<AiUsageLog> usages,
    IRepository<AiUserFeedback> feedback,
    IRepository<AiScenario> scenarios,
    IAsyncQueryExecutor queries,
    ICurrentUserService current,
    ITenantContext tenant,
    IUserCredentialValidator identities) : IAiScenarioOperationsService
{
    public async Task<AiScenarioOperationsResponse> QueryAsync(
        AiScenarioOperationsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var observedFrom = DateTimeOffset.UtcNow;
        var access = await AuthorizeAsync(cancellationToken);
        var to = request.To ?? observedFrom;
        if (!request.From.HasValue && to < DateTimeOffset.MinValue.AddDays(30))
            throw new BusinessException(ErrorCode.ValidationFailed, "AI scenario statistics date range is invalid.");
        var from = request.From ?? to.AddDays(-30);
        if (from >= to || to - from > TimeSpan.FromDays(90) || to > observedFrom.AddMinutes(5) ||
            request.PageIndex < 1 || request.PageSize is < 1 or > AiScenarioOperationsContract.MaxPageSize ||
            (long)(request.PageIndex - 1) * request.PageSize > int.MaxValue)
            throw new BusinessException(ErrorCode.ValidationFailed, "AI scenario statistics date range or pagination is invalid.");

        var population = runs.QueryForTenant(access.TargetTenantId)
            .Where(r => r.CreatedAt >= from && r.CreatedAt < to);
        var observations = await ReadBoundedAsync(population.Select(r => new RunObservation(
            r.Id, r.ScenarioId, r.ActorUserId, r.ResponseMessageId, r.Status, r.ErrorCode, r.DurationMilliseconds)),
            AiScenarioOperationsContract.MaxRuns, cancellationToken);
        var runIds = observations.Select(r => r.Id).ToArray();
        IReadOnlyList<UsageObservation> usageRecords = [];
        IReadOnlyList<FeedbackObservation> feedbackRecords = [];
        if (runIds.Length > 0)
        {
            usageRecords = await ReadBoundedAsync(
                from usage in usages.QueryForTenant(access.TargetTenantId)
                join run in runs.QueryForTenant(access.TargetTenantId) on usage.RunId equals run.Id
                where runIds.Contains(run.Id)
                select new UsageObservation(usage.RunId, usage.Status, usage.InputTokens, usage.OutputTokens,
                    usage.EstimatedCost, usage.PricingCurrency),
                AiScenarioOperationsContract.MaxUsages, cancellationToken);

            var eligibleIds = observations.Where(IsFeedbackEligible).Select(r => r.Id).ToArray();
            if (eligibleIds.Length > 0)
                feedbackRecords = await ReadBoundedAsync(
                    from entry in feedback.QueryForTenant(access.TargetTenantId)
                    join run in runs.QueryForTenant(access.TargetTenantId) on entry.RunId equals run.Id
                    where eligibleIds.Contains(run.Id) && entry.UserId == run.ActorUserId &&
                        entry.MessageId == run.ResponseMessageId &&
                        (entry.Rating == AiFeedbackRating.Positive || entry.Rating == AiFeedbackRating.Negative)
                    select new FeedbackObservation(entry.Id, entry.RunId, entry.UserId, entry.MessageId, entry.Rating,
                        entry.UpdatedAt ?? entry.CreatedAt),
                    AiScenarioOperationsContract.MaxFeedback, cancellationToken);
        }

        // Keep the originally observed population and eligibility while later reads may see updates.
        var runById = observations.ToDictionary(r => r.Id);
        var validFeedback = feedbackRecords.Where(f => runById.TryGetValue(f.RunId, out var run) &&
                IsFeedbackEligible(run) && run.ActorUserId == f.UserId && run.ResponseMessageId == f.MessageId)
            .GroupBy(f => f.RunId)
            .Select(g => g.OrderByDescending(f => f.UpdatedAt).ThenByDescending(f => f.Id).First())
            .ToLookup(f => f.RunId);
        var usageByRun = usageRecords.ToLookup(u => u.RunId);
        var groups = observations.GroupBy(r => r.ScenarioId)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).ToArray();
        var page = groups.Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize).ToArray();
        var scenarioIds = page.Where(g => g.Key.HasValue).Select(g => g.Key!.Value).ToArray();
        IReadOnlyList<ScenarioObservation> names = scenarioIds.Length == 0 ? [] : await queries.ToListAsync(
            scenarios.QueryForTenant(access.TargetTenantId).Where(s => scenarioIds.Contains(s.Id))
                .Select(s => new ScenarioObservation(s.Id, s.Name, s.Code)), cancellationToken);
        var namesById = names.ToDictionary(s => s.Id);
        var items = page.Select(group => Summarize(group,
            group.Key.HasValue ? namesById.GetValueOrDefault(group.Key.Value) : null, usageByRun, validFeedback)).ToArray();

        if (runIds.Length > 0)
        {
            var survivingRuns = await queries.LongCountAsync(
                runs.QueryForTenant(access.TargetTenantId).Where(r => runIds.Contains(r.Id)), cancellationToken);
            if (survivingRuns != runIds.LongLength)
                throw new BusinessException(ErrorCode.Conflict, "AI scenario statistics changed during observation. Please query again.");
            if (names.Count > 0 && await queries.LongCountAsync(
                    scenarios.QueryForTenant(access.TargetTenantId).Where(s => scenarioIds.Contains(s.Id)), cancellationToken) != names.Count)
                throw new BusinessException(ErrorCode.Conflict, "AI scenario metadata changed during observation. Please query again.");
        }
        if (await AuthorizeAsync(cancellationToken) != access)
            throw new BusinessException(ErrorCode.Forbidden, "AI scenario statistics authorization changed.");

        return new AiScenarioOperationsResponse
        {
            From = from, To = to, ObservedFrom = observedFrom, ObservedTo = DateTimeOffset.UtcNow,
            Scenarios = PagedResult<AiScenarioOperationsItemResponse>.Create(items, request.PageIndex, request.PageSize, groups.LongLength)
        };
    }

    private async Task<AccessIdentity> AuthorizeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!current.IsAuthenticated)
            throw new BusinessException(ErrorCode.Unauthorized, "Authentication is required.");
        if (!current.UserId.HasValue || current.UserId == Guid.Empty || !current.TenantId.HasValue || current.TenantId == Guid.Empty)
            throw new BusinessException(ErrorCode.Unauthorized, "A valid AI operations identity is required.");
        if (!tenant.IsResolved || tenant.TenantId is null || tenant.TenantId == Guid.Empty || tenant.IsSystemScopeActive)
            throw new BusinessException(ErrorCode.Forbidden, "An explicit AI operations tenant is required.");

        var actor = await identities.GetAuthenticationStateAsync(current.TenantId.Value, current.UserId.Value, cancellationToken);
        if (actor is null || actor.UserId != current.UserId || actor.TenantId != current.TenantId ||
            (current.SecurityStamp.HasValue && current.SecurityStamp != actor.SecurityStamp))
            throw new BusinessException(ErrorCode.Unauthorized, "The AI operations identity is inactive or stale.");
        var superAdmin = PermissionEvaluation.IsSuperAdmin(actor.Roles);
        if (superAdmin != current.IsSuperAdmin ||
            !current.HasPermission(AiCenterConstants.OperationsViewPermission) ||
            !PermissionEvaluation.HasPermission(true, superAdmin, actor.PermissionCodes, AiCenterConstants.OperationsViewPermission) ||
            (!superAdmin && actor.TenantId != tenant.TenantId))
            throw new BusinessException(ErrorCode.Forbidden, "AI scenario statistics are not authorized.");
        var target = tenant.TenantId.Value;
        if (await identities.ResolveActiveTenantIdAsync(target.ToString("D"), cancellationToken) != target)
            throw new BusinessException(ErrorCode.Forbidden, "The AI operations target tenant is inactive.");
        return new AccessIdentity(actor.UserId, actor.TenantId, target, actor.SecurityStamp);
    }

    private async Task<IReadOnlyList<T>> ReadBoundedAsync<T>(IQueryable<T> query, int limit, CancellationToken cancellationToken)
    {
        var result = await queries.ToListAsync(query.Take(limit + 1), cancellationToken);
        if (result.Count > limit)
            throw new BusinessException(ErrorCode.ValidationFailed, AiScenarioOperationsContract.CapacityMessage);
        return result;
    }

    private static AiScenarioOperationsItemResponse Summarize(
        IGrouping<Guid?, RunObservation> group, ScenarioObservation? name,
        ILookup<Guid, UsageObservation> usageByRun, ILookup<Guid, FeedbackObservation> feedbackByRun)
    {
        var completed = group.LongCount(r => r.Status == AiRunStatus.Completed);
        var failed = group.LongCount(r => r.Status == AiRunStatus.Failed);
        var cancelled = group.LongCount(r => r.Status == AiRunStatus.Cancelled);
        var terminal = completed + failed + cancelled;
        var eligible = group.LongCount(IsFeedbackEligible);
        var ratings = group.SelectMany(r => feedbackByRun[r.Id]).ToArray();
        var positive = ratings.LongCount(f => f.Rating == AiFeedbackRating.Positive);
        var negative = ratings.LongCount(f => f.Rating == AiFeedbackRating.Negative);
        var durations = group.Where(r => IsTerminal(r.Status) && r.DurationMilliseconds is >= 0)
            .Select(r => r.DurationMilliseconds!.Value).Order().ToArray();
        var usage = group.SelectMany(r => usageByRun[r.Id]).ToArray();
        var settled = usage.Where(u => IsTerminal(u.Status)).ToArray();
        return new AiScenarioOperationsItemResponse
        {
            ScenarioId = group.Key, ScenarioName = group.Key is null ? "未绑定场景" : name?.Name ?? "场景不可用",
            ScenarioCode = name?.Code, ScenarioAvailable = name is not null,
            RunCount = group.LongCount(), CompletedRunCount = completed, FailedRunCount = failed, CancelledRunCount = cancelled,
            PendingRunCount = group.LongCount(r => r.Status == AiRunStatus.Pending),
            RunningRunCount = group.LongCount(r => r.Status == AiRunStatus.Running),
            UnknownStatusRunCount = group.LongCount(r => !Enum.IsDefined(r.Status)),
            TerminalRunCount = terminal, TechnicalCompletionRate = Percent(completed, terminal),
            TimeoutFailureCount = group.LongCount(r => r.Status == AiRunStatus.Failed &&
                r.ErrorCode is "run_timeout" or "run_queue_timeout"),
            FeedbackEligibleRunCount = eligible, PositiveFeedbackCount = positive, NegativeFeedbackCount = negative,
            FeedbackCoverageRate = Percent(positive + negative, eligible), PositiveFeedbackRate = Percent(positive, positive + negative),
            DurationSampleCount = durations.LongLength,
            P95DurationMilliseconds = durations.Length == 0 ? null : durations[(int)Math.Ceiling(durations.Length * 0.95m) - 1],
            InputTokens = usage.Sum(u => (long)(u.InputTokens is >= 0 ? u.InputTokens.Value : 0)),
            OutputTokens = usage.Sum(u => (long)(u.OutputTokens is >= 0 ? u.OutputTokens.Value : 0)),
            UnknownUsageInvocationCount = settled.LongCount(u => u.InputTokens is null or < 0 || u.OutputTokens is null or < 0),
            UnknownCostInvocationCount = settled.LongCount(u => !HasKnownCost(u)),
            UnsettledInvocationCount = usage.LongCount(u => u.Status is AiInvocationStatus.Pending or AiInvocationStatus.Running),
            UnknownStatusInvocationCount = usage.LongCount(u => !Enum.IsDefined(u.Status)),
            EstimatedCosts = settled.Where(HasKnownCost).GroupBy(u => u.Currency!)
                .Select(g => new AiCurrencyCostResponse { Currency = g.Key, Amount = g.Sum(u => u.Cost!.Value) })
                .OrderBy(c => c.Currency, StringComparer.Ordinal).ToArray()
        };
    }

    private static bool HasKnownCost(UsageObservation usage) => usage.Cost is >= 0 && usage.Currency is { Length: 3 } &&
        usage.Currency.All(c => c is >= 'A' and <= 'Z');
    private static bool IsFeedbackEligible(RunObservation run) => run.Status == AiRunStatus.Completed && run.ResponseMessageId.HasValue;
    private static bool IsTerminal(AiRunStatus status) => status is AiRunStatus.Completed or AiRunStatus.Failed or AiRunStatus.Cancelled;
    private static bool IsTerminal(AiInvocationStatus status) => status is AiInvocationStatus.Completed or AiInvocationStatus.Failed or AiInvocationStatus.Cancelled;
    private static decimal? Percent(long numerator, long denominator) => denominator == 0 ? null :
        decimal.Round(numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);

    private sealed record AccessIdentity(Guid UserId, Guid IdentityTenantId, Guid TargetTenantId, Guid SecurityStamp);
    private sealed record RunObservation(Guid Id, Guid? ScenarioId, Guid ActorUserId, Guid? ResponseMessageId,
        AiRunStatus Status, string? ErrorCode, long? DurationMilliseconds);
    private sealed record UsageObservation(Guid RunId, AiInvocationStatus Status, int? InputTokens, int? OutputTokens, decimal? Cost, string? Currency);
    private sealed record FeedbackObservation(Guid Id, Guid RunId, Guid UserId, Guid MessageId, AiFeedbackRating Rating, DateTimeOffset UpdatedAt);
    private sealed record ScenarioObservation(Guid Id, string Name, string Code);
}
