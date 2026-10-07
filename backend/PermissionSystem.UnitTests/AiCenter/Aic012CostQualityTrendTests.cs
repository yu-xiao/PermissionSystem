using System.Text.Json;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic012CostQualityTrendTests
{
    [Fact]
    public async Task Trend_UsesUtcUsageTimeClippedBucketsAndHalfOpenWindow()
    {
        var f = new CostQualityFixture(); var from = f.Core.From.AddHours(-12); var to = f.Core.To;
        var first = f.Add(); first.CreatedAt = from.ToOffset(TimeSpan.FromHours(8));
        var second = f.Add(); second.CreatedAt = f.Core.From.ToOffset(TimeSpan.FromHours(-5));
        f.Add().CreatedAt = to; f.Add().CreatedAt = from.AddTicks(-1);
        foreach (var run in f.Core.Runs.Items) run.CreatedAt = from.AddYears(-1);
        var result = await f.Service().QueryTrendAsync(new() { From = from, To = to });
        Assert.Equal(2, result.Population.InvocationCount); Assert.Equal(2, result.Daily.Count);
        Assert.Equal(DateOnly.FromDateTime(from.UtcDateTime), result.Daily[0].Date);
        Assert.Equal(from, result.Daily[0].BucketFrom); Assert.True(result.Daily[0].IsPartialDay);
        Assert.Equal(f.Core.From, result.Daily[0].BucketTo); Assert.Equal(1, result.Daily[0].Population.InvocationCount);
        Assert.False(result.Daily[1].IsPartialDay); Assert.Equal(to, result.Daily[1].BucketTo);
        Assert.Equal(1, result.Daily[1].Population.InvocationCount);
        Assert.All(result.Daily, day => { Assert.Equal(TimeSpan.Zero, day.BucketFrom.Offset); Assert.True(day.BucketFrom < day.BucketTo); });
        Assert.Equal(2, f.Core.Queries.ExecutionCount);
    }

    [Theory]
    [InlineData(1, false, 1)]
    [InlineData(30, false, 30)]
    [InlineData(90, false, 90)]
    [InlineData(90, true, 91)]
    public async Task Trend_EmptyDaysAreBoundedAndUnknownRatiosAreNotZero(int days, bool partial, int buckets)
    {
        var f = new CostQualityFixture(); var to = f.Core.To.AddHours(partial ? -12 : 0);
        var result = await f.Service().QueryTrendAsync(new() { From = to.AddDays(-days), To = to });
        Assert.Equal(buckets, result.Daily.Count); Assert.Empty(result.Currencies.Items);
        Assert.All(result.Daily, day =>
        {
            Assert.Equal(0, day.Population.InvocationCount); Assert.Equal(17, day.Issues.Count);
            Assert.All(day.Issues, issue => Assert.Equal(0, issue.Count));
            Assert.Null(day.InputComparison.WeightedRatioPercentage); Assert.Null(day.OutputLimitComparison.WeightedRatioPercentage);
        });
        Assert.Equal(partial, result.Daily[0].IsPartialDay); Assert.Equal(partial, result.Daily[^1].IsPartialDay);
    }

    [Fact]
    public async Task Trend_DailyAdditiveFieldsConserveTheSameWindowAndRatiosAreWeighted()
    {
        var f = new CostQualityFixture(); var from = f.Core.From.AddDays(-1);
        var a = f.Add(); a.CreatedAt = from; a.InputTokens = a.EstimatedInputTokens = 1;
        var b = f.Add(); b.InputTokens = 1; b.EstimatedInputTokens = 3;
        var fallback = f.Add(AiInvocationStatus.Failed); fallback.InputTokens = null; fallback.EstimatedCost = null;
        var cancelled = f.Add(AiInvocationStatus.Cancelled); cancelled.OutputTokens = -1;
        f.Add(AiInvocationStatus.Pending); f.Add(AiInvocationStatus.Running); f.Add((AiInvocationStatus)99);
        var zero = f.Add(); zero.EstimatedInputTokens = zero.EstimatedOutputTokens = 0;
        var result = await f.Service().QueryTrendAsync(new() { From = from, To = f.Core.To });
        AssertConserved(result.Population, result.Daily.Select(d => d.Population));
        AssertConserved(result.Basis, result.Daily.Select(d => d.Basis));
        AssertConserved(result.TotalTokens, result.Daily.Select(d => d.TotalTokens));
        AssertConserved(result.InputComparison, result.Daily.Select(d => d.InputComparison));
        AssertConserved(result.OutputLimitComparison, result.Daily.Select(d => d.OutputLimitComparison));
        foreach (var issue in result.Issues) Assert.Equal(issue.Count, result.Daily.Sum(d => d.Issues.Single(i => i.Code == issue.Code).Count));
        var samples = result.Daily.Select(d => d.InputComparison).ToArray();
        Assert.Equal(decimal.Round(samples.Sum(s => s.RecordedTokens) * 100m / samples.Sum(s => s.EstimatedTokens), 2,
            MidpointRounding.AwayFromZero), result.InputComparison.WeightedRatioPercentage);
        var paired = new CostQualityFixture(); var x = paired.Add(); x.CreatedAt = from; x.InputTokens = x.EstimatedInputTokens = 1;
        var y = paired.Add(); y.InputTokens = 1; y.EstimatedInputTokens = 3;
        Assert.Equal(50m, (await paired.Service().QueryTrendAsync(new() { From = from, To = paired.Core.To })).InputComparison.WeightedRatioPercentage);
    }

    [Fact]
    public async Task Trend_DirectionsShareRoundedComparableSamplesAndPreserveAbsoluteDifference()
    {
        var f = new CostQualityFixture();
        var above = f.Add(); above.InputTokens = 1; above.OutputTokens = 0; above.InputTokenPricePerMillion = .5m; above.EstimatedCost = .000002m;
        var below = f.Add(); below.InputTokens = 1; below.OutputTokens = 0; below.InputTokenPricePerMillion = .5m; below.EstimatedCost = 0;
        f.Add().EstimatedCost = null; f.Add().InputTokenPricePerMillion = null;
        f.Add(); var zero = f.Add(); zero.InputTokenPricePerMillion = zero.OutputTokenPricePerMillion = zero.EstimatedCost = 0;
        f.Add().PricingCurrency = "bad"; f.Add().PricingCurrency = null;
        var result = await f.Service().QueryTrendAsync(Request(f)); var group = Assert.Single(result.Currencies.Items);
        Assert.Equal(1, group.StoredAboveRecomputedCount); Assert.Equal(1, group.StoredBelowRecomputedCount);
        Assert.Equal(2, group.Summary.ConsistentCostCount); Assert.Equal(4, group.Summary.ComparableCostCount);
        Assert.Equal(2, group.Summary.UncomparableCostCount); Assert.Equal(0m, group.Summary.DifferenceCost);
        Assert.Equal(.000002m, group.Summary.AbsoluteDifferenceCost);
        Assert.Equal(group.Summary.ComparableCostCount, group.StoredAboveRecomputedCount + group.StoredBelowRecomputedCount + group.Summary.ConsistentCostCount);
        Assert.Equal(8, result.Population.TerminalCount);
    }

    [Fact]
    public async Task Trend_PaginationDoesNotChangeTheWindowAndSummaryV1RemainsUnchanged()
    {
        var f = new CostQualityFixture(); f.Add(); f.Add().PricingCurrency = "CNY";
        var service = f.Service(); var old = await service.QueryAsync(f.Request);
        var trend = await service.QueryTrendAsync(new() { From = f.Core.From, To = f.Core.To, PageSize = 1, PageIndex = 2 });
        Assert.Equal(old.Population, trend.Population); Assert.Equal(old.InputComparison, trend.InputComparison);
        Assert.Equal(old.Basis, trend.Basis); Assert.Equal(old.Currencies.Items.Single(c => c.Currency == "USD"), Assert.Single(trend.Currencies.Items).Summary);
        Assert.Equal(2, trend.Currencies.TotalCount); Assert.Equal(2, Assert.Single(trend.Daily).Population.InvocationCount);
        var emptyPage = await service.QueryTrendAsync(new() { From = f.Core.From, To = f.Core.To, PageIndex = int.MaxValue, PageSize = 50 });
        Assert.Empty(emptyPage.Currencies.Items); Assert.Equal(trend.Population, emptyPage.Population);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(old, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.False(json.RootElement.TryGetProperty("daily", out _)); Assert.False(json.RootElement.TryGetProperty("grouping", out _));
        Assert.Equal(6, f.Rates.Calls.Count); Assert.All(f.Rates.Calls, call => Assert.StartsWith("ai-cost-quality:", call.Policy));
    }

    [Fact]
    public async Task Trend_EncodesMoneyExactlyAndDoesNotExposeOrWriteSourceFields()
    {
        var f = new CostQualityFixture(); var a = f.Add(); a.EstimatedCost = 999999999999.999999m;
        a.ModelName = a.ProviderRequestId = a.ErrorCode = "DO_NOT_RETURN"; f.Add().EstimatedCost = a.EstimatedCost;
        var before = JsonSerializer.Serialize(a); var result = await f.Service().QueryTrendAsync(Request(f));
        var text = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var json = JsonDocument.Parse(text);
        Assert.Equal("1999999999999.999998", json.RootElement.GetProperty("currencies").GetProperty("items")[0].GetProperty("summary").GetProperty("storedCost").GetString());
        foreach (var excluded in new[] { "DO_NOT_RETURN", "runId", "usageId", "userId", "modelName", "inputTokenPricePerMillion", a.Id.ToString(), a.RunId.ToString() }) Assert.DoesNotContain(excluded, text);
        Assert.Equal(before, JsonSerializer.Serialize(a)); Assert.Equal("UTC", result.BucketTimezone); Assert.Equal(AiCostQualityTrendContract.Grouping, result.Grouping);
    }

    [Theory]
    [InlineData("usage-tenant")]
    [InlineData("run-tenant")]
    [InlineData("usage-deleted")]
    [InlineData("run-deleted")]
    [InlineData("orphan")]
    public async Task Trend_ExcludesInvisibleAssociationWithoutFillingHistory(string kind)
    {
        var f = new CostQualityFixture(); var row = f.Add(); var run = f.Core.Runs.Items.Single();
        if (kind == "usage-tenant") row.TenantId = Guid.NewGuid();
        if (kind == "run-tenant") run.TenantId = Guid.NewGuid();
        if (kind == "usage-deleted") row.IsDeleted = true;
        if (kind == "run-deleted") run.IsDeleted = true;
        if (kind == "orphan") row.RunId = Guid.NewGuid();
        Assert.Equal(0, (await f.Service().QueryTrendAsync(Request(f))).Population.InvocationCount);
    }

    [Theory]
    [InlineData("cost")]
    [InlineData("date")]
    [InlineData("run-deleted")]
    [InlineData("new-row")]
    [InlineData("revoked")]
    [InlineData("stamp")]
    [InlineData("inactive-final")]
    public async Task Trend_RejectsChangedObservationOrFreshAccess(string kind)
    {
        var f = new CostQualityFixture(); var row = f.Add();
        f.Core.Queries.AfterList = read =>
        {
            if (kind == "inactive-final") { if (read == 2) f.Core.Identities.ActiveTenants.Clear(); return; }
            if (read != 1) return;
            if (kind == "cost") row.EstimatedCost++;
            if (kind == "date") row.CreatedAt = row.CreatedAt.AddDays(-1);
            if (kind == "run-deleted") f.Core.Runs.Items.Single().IsDeleted = true;
            if (kind == "new-row") f.Add();
            if (kind == "revoked") f.Core.Identities.Actor = f.Core.Identities.Actor! with { PermissionCodes = [] };
            if (kind == "stamp") f.Core.Identities.Actor = f.Core.Identities.Actor! with { SecurityStamp = Guid.NewGuid() };
        };
        var error = await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryTrendAsync(Request(f)));
        Assert.Equal(kind is "revoked" or "stamp" or "inactive-final" ? ErrorCode.Forbidden : ErrorCode.Conflict, error.ErrorCode);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("permission")]
    [InlineData("revoked")]
    [InlineData("system")]
    [InlineData("cross-tenant")]
    [InlineData("implicit-super")]
    public async Task Trend_InvalidAccessIsRejectedBeforeReadingOrRateAdmission(string kind)
    {
        var f = new CostQualityFixture();
        if (kind == "anonymous") f.Core.Current.UserId = null;
        if (kind == "permission") f.Core.Current = new(permissions: []);
        if (kind == "revoked") f.Core.Identities.Actor = f.Core.Identities.Actor! with { PermissionCodes = [] };
        if (kind == "system") f.Core.Tenant.IsSystemScopeActive = true;
        if (kind == "cross-tenant") f.Core.Tenant.TenantId = Guid.NewGuid();
        if (kind == "implicit-super") { f.Core.Current.IsSuperAdmin = true; f.Core.Identities.Actor = f.Core.Identities.Actor! with { Roles = [SystemBuiltinConstants.SuperAdminRoleCode] }; }
        await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryTrendAsync(Request(f)));
        Assert.Equal(0, f.Core.Queries.ExecutionCount); Assert.Empty(f.Rates.Calls);
    }

    [Fact]
    public async Task Trend_SuperAdminStillRequiresAnExplicitActiveTarget()
    {
        var f = new CostQualityFixture(); var target = Guid.NewGuid(); f.Add().TenantId = target; f.Core.Runs.Items.Single().TenantId = target;
        var context = new TenantContext(); context.SetTenant(target, "Header"); f.Tenant = context;
        f.Core.Current.IsSuperAdmin = true; f.Core.Identities.Actor = f.Core.Identities.Actor! with { Roles = [SystemBuiltinConstants.SuperAdminRoleCode] };
        f.Core.Identities.ActiveTenants.Add(target);
        Assert.Equal(1, (await f.Service().QueryTrendAsync(Request(f))).Population.InvocationCount);
    }

    [Theory]
    [InlineData(50000, true)]
    [InlineData(50001, false)]
    public async Task Trend_CapacityIsWholeWindowNotPerDay(int count, bool allowed)
    {
        var f = new CostQualityFixture(); var run = f.Core.AddRun(AiRunStatus.Completed);
        for (var i = 0; i < count; i++) f.Add(run: run).CreatedAt = f.Core.From.AddMinutes(i % 1440);
        if (allowed) Assert.Equal(count, (await f.Service().QueryTrendAsync(Request(f))).Population.InvocationCount);
        else Assert.Equal(AiCostQualityContract.CapacityMessage, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryTrendAsync(Request(f)))).Message);
    }

    [Fact]
    public async Task Trend_InvalidParametersSharedRatesCancellationAndDeadlineRemainEnforced()
    {
        var f = new CostQualityFixture();
        foreach (var query in new AiCostQualityTrendQuery[] { new() { PageIndex = 0 }, new() { PageSize = 51 }, new() { From = f.Core.To, To = f.Core.To }, new() { From = f.Core.To.AddDays(-91), To = f.Core.To }, new() { To = DateTimeOffset.MinValue }, new() { To = f.Clock.GetUtcNow().AddMinutes(6) } })
            Assert.Equal(ErrorCode.ValidationFailed, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryTrendAsync(query))).ErrorCode);
        Assert.Empty(f.Rates.Calls); await f.Service().QueryAsync(f.Request); f.Rates.DenyAt = 3;
        Assert.Equal(ErrorCode.TooManyRequests, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryTrendAsync(Request(f)))).ErrorCode);
        Assert.Equal("ai-cost-quality:actor", f.Rates.Calls[^1].Policy); Assert.Equal(2, f.Core.Queries.ExecutionCount);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CostQualityFixture().Service().QueryTrendAsync(new(), cancelled.Token));
        var timed = new CostQualityFixture(); timed.Add(); timed.Core.Queries.AfterList = _ => timed.Clock.Advance(TimeSpan.FromSeconds(11));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timed.Service().QueryTrendAsync(Request(timed)));
        var overflow = new CostQualityFixture(); for (var i = 0; i < 3; i++) overflow.Add().EstimatedCost = decimal.MaxValue / 2;
        Assert.Equal(ErrorCode.ValidationFailed, (await Assert.ThrowsAsync<BusinessException>(() => overflow.Service().QueryTrendAsync(Request(overflow)))).ErrorCode);
    }

    private static AiCostQualityTrendQuery Request(CostQualityFixture f) => new() { From = f.Core.From, To = f.Core.To };
    private static void AssertConserved<T>(T window, IEnumerable<T> daily)
    {
        var days = daily.ToArray();
        foreach (var property in typeof(T).GetProperties().Where(p => p.PropertyType == typeof(long)))
            Assert.Equal((long)property.GetValue(window)!, days.Sum(d => (long)property.GetValue(d)!));
    }
}
