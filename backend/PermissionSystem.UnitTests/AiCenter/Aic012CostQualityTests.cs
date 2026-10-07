using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic012CostQualityTests
{
    [Fact]
    public async Task Query_MatchesHistoricalSettlementAndReturnsOnlySafeAggregatesWithoutWrites()
    {
        var f = new CostQualityFixture(); var row = f.Add();
        row.ModelName = row.ProviderRequestId = row.FinishReason = row.ErrorCode = "DO_NOT_RETURN";
        row.SettleCost(); var original = JsonSerializer.Serialize(row);
        var result = await f.Service().QueryAsync(f.Request);
        Assert.Equal(1, result.Population.ConsistentCostCount); Assert.Equal(1, result.Basis.RecordedBothCount);
        var money = Assert.Single(result.Currencies.Items);
        Assert.Equal(.00005m, money.StoredCost); Assert.Equal(money.StoredCost, money.RecomputedCost);
        Assert.Equal(0m, money.AbsoluteDifferenceCost); Assert.Equal(original, JsonSerializer.Serialize(row));
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var value in new[] { "DO_NOT_RETURN", "providerConfigId", "runId", "inputTokenPricePerMillion", "providerRequestId", "modelName", "userId", "reservedCost", row.Id.ToString(), row.RunId.ToString() }) Assert.DoesNotContain(value, json);
        Assert.Equal(AiCostQualityContract.Scope, result.Scope); Assert.Equal(AiCostQualityContract.CostBasis, result.CostBasis);
        Assert.Equal(2, f.Core.Queries.ExecutionCount);
        Assert.Collection(f.Rates.Calls, a => { Assert.Equal("ai-cost-quality:actor", a.Policy); Assert.Equal(6, a.Limit); },
            t => { Assert.Equal("ai-cost-quality:tenant", t.Policy); Assert.Equal(12, t.Limit); });
    }

    [Theory]
    [InlineData(10, 20, 1, 0, 0, .00005)]
    [InlineData(null, 20, 0, 1, 0, .00014)]
    [InlineData(10, null, 0, 1, 0, .00041)]
    [InlineData(null, null, 0, 0, 1, .0005)]
    [InlineData(-1, 20, 0, 1, 0, .00014)]
    public async Task Query_FollowsPerComponentFallbackWithoutClaimingTokenOrigin(int? input, int? output,
        long recorded, long mixed, long estimated, double cost)
    {
        var f = new CostQualityFixture(); var row = f.Add(); row.InputTokens = input; row.OutputTokens = output; row.SettleCost();
        var response = await f.Service().QueryAsync(f.Request);
        Assert.Equal(new AiCostQualityBasis(recorded, mixed, estimated, 0), response.Basis);
        Assert.Equal((decimal)cost, Assert.Single(response.Currencies.Items).RecomputedCost);
        Assert.Equal(1, response.Population.ConsistentCostCount);
    }

    [Fact]
    public async Task Query_PartitionsTerminalUnsettledUnknownAndKeepsZeroDistinctFromMissing()
    {
        var f = new CostQualityFixture();
        foreach (var status in Enum.GetValues<AiInvocationStatus>().Append((AiInvocationStatus)99))
        { var row = f.Add(status); row.InputTokens = row.OutputTokens = 0; row.InputTokenPricePerMillion = row.OutputTokenPricePerMillion = 0; row.EstimatedCost = 0; }
        var response = await f.Service().QueryAsync(f.Request);
        Assert.Equal(new AiCostQualityPopulation(6, 3, 2, 1, 3, 3, 0, 0), response.Population);
        Assert.Equal(3, response.InputComparison.SampleCount); Assert.Equal(0m, response.InputComparison.WeightedRatioPercentage);
        Assert.Equal(0m, Assert.Single(response.Currencies.Items).StoredCost);
        var empty = await new CostQualityFixture().Service().QueryAsync(new());
        Assert.Empty(empty.Currencies.Items); Assert.Null(empty.InputComparison.WeightedRatioPercentage);
        Assert.Equal(TimeSpan.FromDays(30), empty.To - empty.From);
    }

    [Theory]
    [InlineData("missing-input", "MissingInputTokens")]
    [InlineData("negative-input", "NegativeInputTokens")]
    [InlineData("missing-estimate", "MissingEstimatedInputTokens")]
    [InlineData("negative-estimate", "NegativeEstimatedInputTokens")]
    [InlineData("missing-price", "MissingPrice")]
    [InlineData("negative-price", "NegativePrice")]
    [InlineData("missing-currency", "MissingCurrency")]
    [InlineData("bad-currency", "InvalidCurrency")]
    [InlineData("missing-cost", "MissingStoredCost")]
    [InlineData("negative-cost", "NegativeStoredCost")]
    public async Task Query_ReportsMissingAndInvalidWithoutRepairingFields(string kind, string issue)
    {
        var f = new CostQualityFixture(); var row = f.Add();
        switch (kind)
        {
            case "missing-input": row.InputTokens = null; break;
            case "negative-input": row.InputTokens = -1; break;
            case "missing-estimate": row.EstimatedInputTokens = null; break;
            case "negative-estimate": row.EstimatedInputTokens = -1; break;
            case "missing-price": row.InputTokenPricePerMillion = null; break;
            case "negative-price": row.InputTokenPricePerMillion = -1; break;
            case "missing-currency": row.PricingCurrency = null; break;
            case "bad-currency": row.PricingCurrency = "usd"; break;
            case "missing-cost": row.EstimatedCost = null; break;
            case "negative-cost": row.EstimatedCost = -1; break;
        }
        var before = JsonSerializer.Serialize(row); var result = await f.Service().QueryAsync(f.Request);
        Assert.Equal(1, result.Issues.Single(i => i.Code == issue).Count); Assert.Equal(before, JsonSerializer.Serialize(row));
        Assert.Equal(1, result.Population.ComparableCostCount + result.Population.UncomparableCostCount);
        if (kind is "missing-currency" or "bad-currency") Assert.Empty(result.Currencies.Items);
        if (kind is "missing-price" or "negative-price" or "missing-cost" or "negative-cost") Assert.Null(Assert.Single(result.Currencies.Items).StoredCost);
        if (kind is "missing-estimate" or "negative-estimate") Assert.Equal(1, result.Population.ConsistentCostCount);
    }

    [Fact]
    public async Task Query_KeepsUnrecomputableHistoricalReservationAndOverlappingDiagnosticsUnknown()
    {
        var f = new CostQualityFixture(); var row = f.Add(); row.InputTokens = null; row.EstimatedInputTokens = -1;
        row.InputTokenPricePerMillion = null; row.OutputTokenPricePerMillion = -1; row.EstimatedCost = 7; row.ReservedCost = 9;
        var result = await f.Service().QueryAsync(f.Request);
        Assert.Equal(1, result.Basis.UnusableTokenPairCount); Assert.Equal(1, result.Population.UncomparableCostCount);
        Assert.True(result.Issues.Sum(i => i.Count) > 1); Assert.Null(Assert.Single(result.Currencies.Items).StoredCost);
        Assert.Equal(7, row.EstimatedCost); Assert.Equal(9, row.ReservedCost);
    }

    [Fact]
    public async Task Query_UsesPairedSamplesAndOutputLimitsWithLongTotalsAndWeightedRatios()
    {
        var f = new CostQualityFixture(); var a = f.Add(); a.InputTokens = 10; a.EstimatedInputTokens = 20;
        var b = f.Add(); b.InputTokens = 50; b.EstimatedInputTokens = 100;
        var zero = f.Add(); zero.EstimatedInputTokens = zero.EstimatedOutputTokens = 0;
        var negative = f.Add(); negative.InputTokens = -1; negative.OutputTokens = null;
        var over = f.Add(); over.InputTokens = int.MaxValue; over.EstimatedInputTokens = int.MaxValue;
        over.OutputTokens = 300; over.EstimatedOutputTokens = 100; over.TotalTokens = int.MaxValue;
        var response = await f.Service().QueryAsync(f.Request);
        Assert.Equal(3, response.InputComparison.SampleCount); Assert.Equal((long)int.MaxValue + 60, response.InputComparison.RecordedTokens);
        Assert.Equal(-60, response.InputComparison.DifferenceTokens); Assert.Equal(60, response.InputComparison.AbsoluteDifferenceTokens);
        Assert.Equal(1, response.InputComparison.ZeroEstimatePairCount); Assert.Equal(1, response.OutputLimitComparison.ZeroEstimatePairCount);
        Assert.Equal(1, response.OutputLimitComparison.AboveEstimateCount); Assert.Equal(4, response.TotalTokens.ComparableCount);
        Assert.Equal(2, response.TotalTokens.DifferentCount);
        var small = new CostQualityFixture(); var token = small.Add(); token.OutputTokens = 300; token.EstimatedOutputTokens = 100;
        Assert.Equal(300m, (await small.Service().QueryAsync(small.Request)).OutputLimitComparison.WeightedRatioPercentage);
    }

    [Fact]
    public async Task Query_PreservesRoundingCurrencyDenominatorsAndAbsoluteDifferences()
    {
        var f = new CostQualityFixture();
        var a = f.Add(); a.InputTokens = 1; a.OutputTokens = 0; a.InputTokenPricePerMillion = .5m; a.EstimatedCost = .000002m;
        var b = f.Add(); b.InputTokens = 1; b.OutputTokens = 0; b.InputTokenPricePerMillion = .5m; b.EstimatedCost = 0;
        f.Add().EstimatedCost = null; f.Add().PricingCurrency = "CNY";
        var result = await f.Service().QueryAsync(new() { From = f.Request.From, To = f.Request.To, PageSize = 1, PageIndex = 2 });
        var currency = Assert.Single(result.Currencies.Items); Assert.Equal("USD", currency.Currency);
        Assert.Equal(2, currency.ComparableCostCount); Assert.Equal(.000002m, currency.StoredCost);
        Assert.Equal(.000002m, currency.RecomputedCost); Assert.Equal(0m, currency.DifferenceCost);
        Assert.Equal(.000002m, currency.AbsoluteDifferenceCost); Assert.Equal(1, currency.UncomparableCostCount);
        Assert.Equal(2, result.Currencies.TotalCount); Assert.Equal(4, result.Population.TerminalCount);
        Assert.Empty((await f.Service().QueryAsync(new() { From = f.Request.From, To = f.Request.To, PageIndex = int.MaxValue, PageSize = 50 })).Currencies.Items);
    }

    [Fact]
    public async Task Query_UsesWeightedTokenRatioInsteadOfAveragingRowPercentages()
    {
        var f = new CostQualityFixture(); var first = f.Add(); first.InputTokens = first.EstimatedInputTokens = 1;
        var second = f.Add(); second.InputTokens = 1; second.EstimatedInputTokens = 3;
        Assert.Equal(50m, (await f.Service().QueryAsync(f.Request)).InputComparison.WeightedRatioPercentage);
        var single = new CostQualityFixture(); var row = single.Add(); row.InputTokens = 1; row.EstimatedInputTokens = 3;
        Assert.Equal(33.33m, (await single.Service().QueryAsync(single.Request)).InputComparison.WeightedRatioPercentage);
    }

    [Fact]
    public async Task Query_DistinguishesSingleRowOverflowFromWholeAggregateOverflow()
    {
        var f = new CostQualityFixture(); var row = f.Add(); row.InputTokens = int.MaxValue; row.InputTokenPricePerMillion = decimal.MaxValue;
        var result = await f.Service().QueryAsync(f.Request); Assert.Equal(1, result.Issues.Single(i => i.Code == "ArithmeticOverflow").Count);
        Assert.Null(Assert.Single(result.Currencies.Items).RecomputedCost);
        var large = new CostQualityFixture();
        for (var i = 0; i < 3; i++) { var usage = large.Add(); usage.InputTokenPricePerMillion = 0; usage.OutputTokenPricePerMillion = 0; usage.EstimatedCost = decimal.MaxValue / 2; }
        Assert.Equal(ErrorCode.ValidationFailed, (await Assert.ThrowsAsync<BusinessException>(() => large.Service().QueryAsync(large.Request))).ErrorCode);
    }

    [Fact]
    public async Task Query_EncodesAggregatedDecimalMoneyAsExactStringsForBrowsers()
    {
        var f = new CostQualityFixture(); f.Add().EstimatedCost = 999999999999.999999m; f.Add().EstimatedCost = 999999999999.999999m;
        var result = await f.Service().QueryAsync(f.Request);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("1999999999999.999998", json.RootElement.GetProperty("currencies").GetProperty("items")[0].GetProperty("storedCost").GetString());
        Assert.Equal(1999999999999.999998m, Assert.Single(result.Currencies.Items).StoredCost);
    }

    [Theory]
    [InlineData("usage-tenant")]
    [InlineData("run-tenant")]
    [InlineData("usage-deleted")]
    [InlineData("run-deleted")]
    [InlineData("orphan")]
    [InlineData("upper-bound")]
    public async Task Query_ExcludesInvisibleRowsAndUsesUsageTimeRatherThanRunTime(string kind)
    {
        var f = new CostQualityFixture(); var row = f.Add(); var run = f.Core.Runs.Items.Single(); run.CreatedAt = f.Core.From.AddYears(-1);
        switch (kind)
        {
            case "usage-tenant": row.TenantId = Guid.NewGuid(); break;
            case "run-tenant": run.TenantId = Guid.NewGuid(); break;
            case "usage-deleted": row.IsDeleted = true; break;
            case "run-deleted": run.IsDeleted = true; break;
            case "orphan": row.RunId = Guid.NewGuid(); break;
            case "upper-bound": row.CreatedAt = f.Request.To!.Value; break;
        }
        Assert.Equal(0, (await f.Service().QueryAsync(f.Request)).Population.InvocationCount);
        var included = new CostQualityFixture(); var usage = included.Add(); included.Core.Runs.Items.Single().CreatedAt = included.Core.From.AddYears(-1);
        usage.CreatedAt = included.Request.From!.Value;
        Assert.Equal(1, (await included.Service().QueryAsync(included.Request)).Population.InvocationCount);
    }

    [Theory]
    [InlineData("cost")]
    [InlineData("usage-deleted")]
    [InlineData("run-deleted")]
    [InlineData("run-tenant")]
    [InlineData("association")]
    [InlineData("new-row")]
    [InlineData("revoked")]
    [InlineData("stamp")]
    public async Task Query_RejectsChangedSourceAndFreshAuthorization(string kind)
    {
        var f = new CostQualityFixture(); var row = f.Add();
        f.Core.Queries.AfterList = read =>
        {
            if (read != 1) return;
            switch (kind)
            {
                case "cost": row.EstimatedCost++; break;
                case "usage-deleted": row.IsDeleted = true; break;
                case "run-deleted": f.Core.Runs.Items.Single().IsDeleted = true; break;
                case "run-tenant": f.Core.Runs.Items.Single().TenantId = Guid.NewGuid(); break;
                case "association": row.RunId = Guid.NewGuid(); break;
                case "new-row": f.Add(); break;
                case "revoked": f.Core.Identities.Actor = f.Core.Identities.Actor! with { PermissionCodes = [] }; break;
                case "stamp": f.Core.Identities.Actor = f.Core.Identities.Actor! with { SecurityStamp = Guid.NewGuid() }; break;
            }
        };
        var error = await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request));
        Assert.Equal(kind is "revoked" or "stamp" ? ErrorCode.Forbidden : ErrorCode.Conflict, error.ErrorCode);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("permission")]
    [InlineData("revoked")]
    [InlineData("inactive")]
    [InlineData("system")]
    [InlineData("cross-tenant")]
    [InlineData("stale-super")]
    public async Task Query_RejectsInvalidAccessBeforeReadingOrAdmission(string kind)
    {
        var f = new CostQualityFixture();
        if (kind == "anonymous") f.Core.Current.UserId = null;
        if (kind == "permission") f.Core.Current = new(permissions: []);
        if (kind == "revoked") f.Core.Identities.Actor = f.Core.Identities.Actor! with { PermissionCodes = [] };
        if (kind == "inactive") f.Core.Identities.ActiveTenants.Clear();
        if (kind == "system") f.Core.Tenant.IsSystemScopeActive = true;
        if (kind == "cross-tenant") f.Core.Tenant.TenantId = Guid.NewGuid();
        if (kind == "stale-super") f.Core.Current.IsSuperAdmin = true;
        await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request));
        Assert.Equal(0, f.Core.Queries.ExecutionCount); Assert.Empty(f.Rates.Calls);
    }

    [Fact]
    public async Task Query_RequiresExplicitSuperTargetAndRechecksFinalReadAuthorization()
    {
        var f = new CostQualityFixture(); var target = Guid.NewGuid(); var row = f.Add(); row.TenantId = target; f.Core.Runs.Items.Single().TenantId = target;
        var context = new TenantContext(); context.SetTenant(target, "Header"); f.Tenant = context;
        f.Core.Current.IsSuperAdmin = true; f.Core.Identities.Actor = f.Core.Identities.Actor! with { Roles = [SystemBuiltinConstants.SuperAdminRoleCode] };
        f.Core.Identities.ActiveTenants.Add(target);
        Assert.Equal(1, (await f.Service().QueryAsync(f.Request)).Population.InvocationCount);
        context.SetTenant(target, "Identity"); await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request));
        var final = new CostQualityFixture(); final.Add(); final.Core.Queries.AfterList = read => { if (read == 2) final.Core.Identities.ActiveTenants.Clear(); };
        await Assert.ThrowsAsync<BusinessException>(() => final.Service().QueryAsync(final.Request));
    }

    [Theory]
    [InlineData(50000, true)]
    [InlineData(50001, false)]
    public async Task Query_WholeWindowCapacityBoundary(int count, bool allowed)
    {
        var f = new CostQualityFixture(); var run = f.Core.AddRun(AiRunStatus.Completed);
        for (var i = 0; i < count; i++) f.Add(run: run);
        if (allowed) Assert.Equal(count, (await f.Service().QueryAsync(f.Request)).Population.InvocationCount);
        else Assert.Equal(AiCostQualityContract.CapacityMessage, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request))).Message);
    }

    [Fact]
    public async Task Query_InvalidWindowPaginationRateCancellationAndDeadline()
    {
        var f = new CostQualityFixture();
        foreach (var query in new AiCostQualityQuery[] { new() { PageIndex = 0 }, new() { PageSize = 51 }, new() { From = f.Core.To, To = f.Core.To }, new() { From = f.Core.To.AddDays(-91), To = f.Core.To }, new() { To = DateTimeOffset.MinValue }, new() { To = DateTimeOffset.UtcNow.AddMinutes(6) } })
            Assert.Equal(ErrorCode.ValidationFailed, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(query))).ErrorCode);
        Assert.Empty(f.Rates.Calls); f.Rates.DenyAt = 2;
        Assert.Equal(ErrorCode.TooManyRequests, (await Assert.ThrowsAsync<BusinessException>(() => f.Service().QueryAsync(f.Request))).ErrorCode);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CostQualityFixture().Service().QueryAsync(new(), cancelled.Token));
        var timed = new CostQualityFixture(); timed.Add(); timed.Core.Queries.AfterList = _ => timed.Clock.Advance(TimeSpan.FromSeconds(11));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timed.Service().QueryAsync(timed.Request));
    }
}

internal sealed class CostQualityFixture
{
    public Aic012Fixture Core { get; } = new();
    public TechnicalExportFixture.ExportRates Rates { get; } = new();
    public ExportClock Clock { get; } = new();
    public ITenantContext Tenant { get; set; }
    public CostQualityFixture() => Tenant = Core.Tenant;
    public AiCostQualityQuery Request => new() { From = Core.From, To = Core.To };
    public AiCostQualityService Service(IAsyncQueryExecutor? queries = null, IRepository<AiRun>? runs = null, IRepository<AiUsageLog>? usages = null) =>
        new(runs ?? Core.Runs, usages ?? Core.Usages, queries ?? Core.Queries, Core.Current, Tenant, Core.Identities, Rates, Clock);
    public AiUsageLog Add(AiInvocationStatus status = AiInvocationStatus.Completed, AiRun? run = null)
    {
        var row = Core.AddUsage(run ?? Core.AddRun(AiRunStatus.Completed), status, 10, 20, .00005m, "USD");
        row.CreatedAt = Core.From.AddHours(1); row.TotalTokens = 30;
        row.EstimatedInputTokens = 100; row.EstimatedOutputTokens = 200;
        row.InputTokenPricePerMillion = 1; row.OutputTokenPricePerMillion = 2;
        return row;
    }
}
