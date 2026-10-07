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

public sealed class Aic012TechnicalExportReceiptSqlTests
{
    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Query_ReadsPersistedExportHashAndExcludesOtherActors()
    {
        await using var f = await SqlFixture.CreateAsync(); await Aic012TechnicalExportSqlTests.GrantAsync(f);
        var file = await Aic012TechnicalExportSqlTests.Service(f).ExportAsync(new() { From = f.Request.From, To = f.Request.To });
        var source = await f.Db.OperationLogs.AsNoTracking().FirstAsync(l => l.Module == "AiTechnicalExport");
        f.Db.OperationLogs.Add(new() { TenantId = source.TenantId, UserId = Guid.NewGuid(), Module = source.Module,
            Method = source.Method, Action = source.Action, RequestMethod = source.RequestMethod, RequestPath = source.RequestPath, RequestBody = source.RequestBody });
        await f.Db.SaveChangesAsync();
        var response = await Service(f).GetAsync(file.ExportId, Window());
        Assert.Equal(2, response.MatchedRecordCount); Assert.True(response.Export.CanVerify);
        var prepared = Assert.Single(response.Receipts, r => r.Outcome == "Prepared");
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file.Content)), prepared.FileSha256);
        Assert.Equal(file.Content.Length, prepared.Bytes);
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Query_RejectsDatabasePermissionRevocation()
    {
        await using var f = await SqlFixture.CreateAsync(); await Aic012TechnicalExportSqlTests.GrantAsync(f);
        await Aic012TechnicalExportSqlTests.Service(f).ExportAsync(new() { From = f.Request.From, To = f.Request.To });
        var error = await Assert.ThrowsAsync<BusinessException>(() => Service(f, new RevokeExecutor(f.Db, f.Role.Id)).QueryAsync(new()));
        Assert.Equal(ErrorCode.Forbidden, error.ErrorCode);
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Query_RejectsSoftDeletionBetweenObservations()
    {
        await using var f = await SqlFixture.CreateAsync(); await Aic012TechnicalExportSqlTests.GrantAsync(f);
        await Aic012TechnicalExportSqlTests.Service(f).ExportAsync(new() { From = f.Request.From, To = f.Request.To });
        var error = await Assert.ThrowsAsync<BusinessException>(() => Service(f, new DeleteExecutor(f)).QueryAsync(new() { To = DateTimeOffset.UtcNow.AddMinutes(1) }));
        Assert.Equal(ErrorCode.Conflict, error.ErrorCode);
    }

    private static AiTechnicalExportReceiptWindow Window() => new() { From = DateTimeOffset.UtcNow.AddDays(-1), To = DateTimeOffset.UtcNow.AddMinutes(1) };
    private static AiTechnicalExportReceiptService Service(SqlFixture f, IAsyncQueryExecutor? executor = null) => new(
        new Repository<OperationLog>(f.Db), executor ?? new EfCoreAsyncQueryExecutor(),
        new(new SqlCurrentUser(f.Actor, f.Role, export: true), f.Tenant, new UserCredentialValidator(f.Db, new PasswordHasher<User>(), f.Tenant)),
        new AllowedRates(), TimeProvider.System);
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
            if (!_deleted)
            {
                _deleted = true;
                await f.Db.OperationLogs.Where(l => l.Module == "AiTechnicalExport").ExecuteUpdateAsync(s => s.SetProperty(l => l.IsDeleted, true), ct);
            }
            return result;
        }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.LongCountAsync(query, ct);
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.AnyAsync(query, ct);
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) => _inner.FirstOrDefaultAsync(query, ct);
    }
}
