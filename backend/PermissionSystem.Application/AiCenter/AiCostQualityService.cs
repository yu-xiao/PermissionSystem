using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiCostQualityService(IRepository<AiRun> runs, IRepository<AiUsageLog> usages,
    IAsyncQueryExecutor queries, ICurrentUserService current, ITenantContext tenant, IUserCredentialValidator identities,
    IDistributedRateLimitService rateLimits, TimeProvider time) : IAiCostQualityService
{
    public Task<AiCostQualityResponse> QueryAsync(AiCostQualityQuery request, CancellationToken ct = default) =>
        QueryCoreAsync<AiCostQualityCalculation, AiCostQualityResponse>(request.From, request.To, request.PageIndex, request.PageSize,
            (rows, _, _, check) => AiCostQualityCalculator.Calculate(rows, check),
            (result, window) => new AiCostQualityResponse(window.TenantId, window.From, window.To, window.ObservedFrom, window.ObservedTo,
                result.Population, result.Basis, result.Issues, result.Input, result.Output, result.Total,
                Page(result.Currencies, request.PageIndex, request.PageSize)), ct);

    public Task<AiCostQualityTrendResponse> QueryTrendAsync(AiCostQualityTrendQuery request, CancellationToken ct = default) =>
        QueryCoreAsync<AiCostQualityTrendCalculation, AiCostQualityTrendResponse>(request.From, request.To, request.PageIndex, request.PageSize, AiCostQualityTrendCalculator.Calculate,
            (result, window) => new AiCostQualityTrendResponse(window.TenantId, window.From, window.To, window.ObservedFrom, window.ObservedTo,
                result.Window.Population, result.Window.Basis, result.Window.Issues, result.Window.Input, result.Window.Output,
                result.Window.Total, result.Daily, Page(result.Window.Distributions, request.PageIndex, request.PageSize)), ct);

    private async Task<TResponse> QueryCoreAsync<TCalculation, TResponse>(DateTimeOffset? requestedFrom, DateTimeOffset? requestedTo,
        int pageIndex, int pageSize, Func<IReadOnlyList<AiCostQualityObservation>, DateTimeOffset, DateTimeOffset, Action, TCalculation> calculate,
        Func<TCalculation, Window, TResponse> respond, CancellationToken cancellation)
    {
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        reading.CancelAfter(TimeSpan.FromSeconds(AiCostQualityContract.ReadSeconds));
        var ct = reading.Token; var observedFrom = time.GetUtcNow();
        var deadline = observedFrom.AddSeconds(AiCostQualityContract.ReadSeconds);
        var access = await AuthorizeAsync(ct);
        var to = requestedTo ?? observedFrom;
        if (!requestedFrom.HasValue && to < DateTimeOffset.MinValue.AddDays(30)) throw InvalidQuery();
        var windowFrom = requestedFrom ?? to.AddDays(-30);
        if (windowFrom >= to || to - windowFrom > TimeSpan.FromDays(90) || to > observedFrom.AddMinutes(5) ||
            pageIndex < 1 || pageSize is < 1 or > 50) throw InvalidQuery();
        await AdmitAsync("ai-cost-quality:actor", access.ActorId, 6, ct);
        await AdmitAsync("ai-cost-quality:tenant", access.TargetTenantId, 12, ct);
        IQueryable<AiCostQualityObservation> Query() =>
            from usage in usages.QueryForTenant(access.TargetTenantId)
            join run in runs.QueryForTenant(access.TargetTenantId) on usage.RunId equals run.Id
            where usage.CreatedAt >= windowFrom && usage.CreatedAt < to
            orderby usage.CreatedAt, usage.Id
            select new AiCostQualityObservation(usage.Id, usage.RunId, usage.CreatedAt, usage.Status,
                usage.InputTokens, usage.OutputTokens, usage.TotalTokens, usage.EstimatedInputTokens, usage.EstimatedOutputTokens,
                usage.InputTokenPricePerMillion, usage.OutputTokenPricePerMillion, usage.PricingCurrency, usage.EstimatedCost);
        var original = await ReadBoundedAsync(Query(), ct);
        await ReauthorizeAsync();
        TCalculation calculation;
        try { calculation = calculate(original, windowFrom, to, CheckDeadline); }
        catch (OverflowException) { throw new BusinessException(ErrorCode.ValidationFailed, "估算质量汇总超出计算范围，请缩短调用记录时间范围。"); }
        var fresh = await ReadBoundedAsync(Query(), ct);
        if (!original.SequenceEqual(fresh)) throw new BusinessException(ErrorCode.Conflict, "估算质量来源已变化，请重新查询。");
        await ReauthorizeAsync(); CheckDeadline();
        var response = respond(calculation, new(access.TargetTenantId, windowFrom, to, observedFrom, time.GetUtcNow()));
        CheckDeadline();
        return response;

        async Task ReauthorizeAsync()
        {
            if (await AuthorizeAsync(ct) != access) throw new BusinessException(ErrorCode.Forbidden, "估算质量查询期间身份或目标发生变化。");
        }
        void CheckDeadline()
        {
            ct.ThrowIfCancellationRequested();
            if (time.GetUtcNow() >= deadline) throw new OperationCanceledException("AI cost quality reading expired.", ct);
        }
    }

    private static PagedResult<T> Page<T>(IReadOnlyList<T> rows, int index, int size)
    {
        var skip = (long)(index - 1) * size;
        var items = skip >= rows.Count ? [] : rows.Skip((int)skip).Take(size).ToArray();
        return PagedResult<T>.Create(items, index, size, rows.Count);
    }
    private sealed record Window(Guid TenantId, DateTimeOffset From, DateTimeOffset To, DateTimeOffset ObservedFrom, DateTimeOffset ObservedTo);

    private static BusinessException InvalidQuery() => new(ErrorCode.ValidationFailed, "调用记录时间或分页范围无效，最多 90 天、每页最多 50 组。");
    private async Task<IReadOnlyList<AiCostQualityObservation>> ReadBoundedAsync(IQueryable<AiCostQualityObservation> query, CancellationToken ct)
    {
        var rows = await queries.ToListAsync(query.Take(AiCostQualityContract.MaxUsages + 1), ct);
        if (rows.Count > AiCostQualityContract.MaxUsages) throw new BusinessException(ErrorCode.ValidationFailed, AiCostQualityContract.CapacityMessage);
        return rows;
    }
    private async Task AdmitAsync(string policy, Guid key, int limit, CancellationToken ct)
    {
        if (!(await rateLimits.TryAcquireAsync(policy, key.ToString("N"), limit, TimeSpan.FromMinutes(1), ct)).IsAcquired)
            throw new BusinessException(ErrorCode.TooManyRequests, "估算质量查询过于频繁，请稍后重试。");
    }
    private async Task<Access> AuthorizeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!current.IsAuthenticated || current.UserId is not Guid actorId || actorId == Guid.Empty ||
            current.TenantId is not Guid identityTenant || identityTenant == Guid.Empty)
            throw new BusinessException(ErrorCode.Unauthorized, "估算质量核验需要有效身份。");
        if (!tenant.IsResolved || tenant.TenantId is not Guid target || target == Guid.Empty || tenant.IsSystemScopeActive)
            throw new BusinessException(ErrorCode.Forbidden, "估算质量核验需要明确活动目标租户。");
        var actor = await identities.GetAuthenticationStateAsync(identityTenant, actorId, ct);
        if (actor is null || actor.UserId != actorId || actor.TenantId != identityTenant ||
            (current.SecurityStamp.HasValue && current.SecurityStamp != actor.SecurityStamp))
            throw new BusinessException(ErrorCode.Unauthorized, "估算质量查询身份已失效或过期。");
        var super = PermissionEvaluation.IsSuperAdmin(actor.Roles);
        if (super != current.IsSuperAdmin || !current.HasPermission(AiCenterConstants.OperationsViewPermission) ||
            !PermissionEvaluation.HasPermission(true, super, actor.PermissionCodes, AiCenterConstants.OperationsViewPermission) ||
            (!super && target != identityTenant) || (super && tenant.Source is not ("Header" or "Request")))
            throw new BusinessException(ErrorCode.Forbidden, "估算质量查询目标未获授权。");
        if (await identities.ResolveActiveTenantIdAsync(target.ToString("D"), ct) != target)
            throw new BusinessException(ErrorCode.Forbidden, "估算质量查询目标已停用。");
        return new(actorId, identityTenant, target, actor.SecurityStamp);
    }
    private sealed record Access(Guid ActorId, Guid IdentityTenantId, Guid TargetTenantId, Guid SecurityStamp);
}
