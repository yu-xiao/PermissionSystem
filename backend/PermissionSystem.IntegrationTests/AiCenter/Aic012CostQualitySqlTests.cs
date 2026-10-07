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

public sealed class Aic012CostQualitySqlTests
{
    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Query_UsesPersistedSnapshotEvenAfterProviderChanges()
    {
        await using var f = await SqlFixture.CreateAsync();
        var usage = await f.Db.AiUsageLogs.SingleAsync(u => u.RunId == f.Completed.Id);
        usage.InputTokenPricePerMillion = 1; usage.OutputTokenPricePerMillion = 2;
        usage.EstimatedInputTokens = 100; usage.EstimatedOutputTokens = 200; usage.SettleCost();
        f.Provider.InputTokenPricePerMillion = 99; f.Provider.OutputTokenPricePerMillion = 99;
        await f.Db.SaveChangesAsync();
        var result = await Service(f).QueryAsync(Window());
        Assert.Equal(2, result.Population.TerminalCount);
        var usd = Assert.Single(result.Currencies.Items, g => g.Currency == "USD");
        Assert.Equal(.00005m, usd.RecomputedCost); Assert.Equal(1, usd.ConsistentCostCount);
        Assert.Null(Assert.Single(result.Currencies.Items, g => g.Currency == "CNY").StoredCost);
    }
    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Query_RejectsDatabaseRevocationDuringObservation()
    {
        await using var f = await SqlFixture.CreateAsync();
        Assert.Equal(ErrorCode.Forbidden, (await Assert.ThrowsAsync<BusinessException>(() => Service(f, new RevokeExecutor(f.Db, f.Role.Id)).QueryAsync(Window()))).ErrorCode);
    }
    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Query_RejectsRunSoftDeletionBetweenReads()
    {
        await using var f = await SqlFixture.CreateAsync();
        Assert.Equal(ErrorCode.Conflict, (await Assert.ThrowsAsync<BusinessException>(() => Service(f, new DeleteExecutor(f)).QueryAsync(Window()))).ErrorCode);
    }
    private static AiCostQualityQuery Window() => new() { From = DateTimeOffset.UtcNow.AddDays(-1), To = DateTimeOffset.UtcNow.AddMinutes(1) };
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
