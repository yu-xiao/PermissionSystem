using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.Queries;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using static PermissionSystem.IntegrationTests.AiCenter.Aic012OperationsSqlTests;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic012CostQualityTrendSqlTests
{
    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Trend_UsesPersistedUtcDatesAndHistoricalSnapshotDirection()
    {
        await using var f = await SqlFixture.CreateAsync();
        var from = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(-2).AddHours(12);
        var to = from.AddDays(1);
        var row = await f.Db.AiUsageLogs.SingleAsync(u => u.RunId == f.Completed.Id);
        await f.Db.AiUsageLogs.Where(u => u.Id == row.Id).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.CreatedAt, from.ToOffset(TimeSpan.FromHours(8)))
            .SetProperty(u => u.InputTokenPricePerMillion, 1m).SetProperty(u => u.OutputTokenPricePerMillion, 2m)
            .SetProperty(u => u.EstimatedCost, .000051m));
        await f.Db.AiProviderConfigs.Where(p => p.Id == f.Provider.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.InputTokenPricePerMillion, 99m));
        var result = await Service(f).QueryTrendAsync(new() { From = from, To = to });
        Assert.Equal(2, result.Daily.Count); Assert.Equal(1, result.Population.InvocationCount);
        Assert.Equal(1, result.Daily[0].Population.InvocationCount); Assert.Equal(0, result.Daily[1].Population.InvocationCount);
        Assert.All(result.Daily, day => Assert.True(day.IsPartialDay));
        var usd = Assert.Single(result.Currencies.Items); Assert.Equal(.00005m, usd.Summary.RecomputedCost);
        Assert.Equal(1, usd.StoredAboveRecomputedCount); Assert.Equal(0, usd.StoredBelowRecomputedCount);
    }
    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Trend_RejectsFreshDatabaseRevocationDuringObservation()
    {
        await using var f = await SqlFixture.CreateAsync();
        Assert.Equal(ErrorCode.Forbidden, (await Assert.ThrowsAsync<BusinessException>(() => Service(f, new RevokeExecutor(f.Db, f.Role.Id)).QueryTrendAsync(Window()))).ErrorCode);
    }
    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Trend_RejectsRunDeletionBetweenTheTwoPopulationReads()
    {
        await using var f = await SqlFixture.CreateAsync();
        Assert.Equal(ErrorCode.Conflict, (await Assert.ThrowsAsync<BusinessException>(() => Service(f, new DeleteExecutor(f)).QueryTrendAsync(Window()))).ErrorCode);
    }
    private static AiCostQualityTrendQuery Window() => new() { From = DateTimeOffset.UtcNow.AddDays(-1), To = DateTimeOffset.UtcNow.AddMinutes(1) };
    private static AiCostQualityService Service(SqlFixture f, IAsyncQueryExecutor? executor = null) => new(
        new Repository<AiRun>(f.Db), new Repository<AiUsageLog>(f.Db), executor ?? new EfCoreAsyncQueryExecutor(),
        new SqlCurrentUser(f.Actor, f.Role), f.Tenant, new UserCredentialValidator(f.Db, new PasswordHasher<User>(), f.Tenant), new AllowedRates(), TimeProvider.System);
    private sealed class AllowedRates : IDistributedRateLimitService
    {
        public Task<RateLimitAcquireResult> TryAcquireAsync(string policyName, string partitionKey, int permitLimit, TimeSpan window, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult(RateLimitAcquireResult.Acquired); }
    }
    private sealed class DeleteExecutor(SqlFixture f) : IAsyncQueryExecutor
    {
        private readonly EfCoreAsyncQueryExecutor _inner = new(); private bool _deleted;
        public async Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        {
            var result = await _inner.ToListAsync(query, ct);
            if (!_deleted) { _deleted = true; await f.Db.AiRuns.Where(r => r.Id == f.Completed.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsDeleted, true), ct); }
            return result;
        }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.LongCountAsync(query, ct);
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.AnyAsync(query, ct);
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.FirstOrDefaultAsync(query, ct);
    }
}
