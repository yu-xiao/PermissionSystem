using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.Locks;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Infrastructure.Queries;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Infrastructure.UnitOfWork;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using static PermissionSystem.IntegrationTests.AiCenter.Aic012OperationsSqlTests;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic012TechnicalExportSqlTests
{
    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Export_UsesDatabasePermissionsAndPersistsTenantBoundHashedReceipt()
    {
        await using var f = await SqlFixture.CreateAsync(); await GrantAsync(f);
        var file = await Service(f).ExportAsync(new() { From = f.Request.From, To = f.Request.To });
        using var doc = JsonDocument.Parse(file.Content);
        Assert.Equal(2, doc.RootElement.GetProperty("manifest").GetProperty("runCount").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("manifest").GetProperty("usageCount").GetInt32());
        var receipts = await f.Db.OperationLogs.AsNoTracking().Where(a => a.TenantId == f.Tenant.TenantId && a.Module == "AiTechnicalExport")
            .OrderBy(a => a.CreatedAt).ToListAsync();
        Assert.Equal(2, receipts.Count); Assert.All(receipts, a => Assert.Equal(f.Actor.Id, a.UserId));
        using var prepared = JsonDocument.Parse(receipts.Single(a => a.Action == "Prepared").RequestBody!);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(file.Content)), prepared.RootElement.GetProperty("fileSha256").GetString());
        Assert.Equal(file.ExportId, prepared.RootElement.GetProperty("exportId").GetGuid());
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Export_DatabaseRoleRevocationAfterRequestedPreventsFile()
    {
        await using var f = await SqlFixture.CreateAsync(); await GrantAsync(f);
        var service = Service(f, new RevokeExecutor(f.Db, f.Role.Id));
        Assert.Equal(ErrorCode.Forbidden, (await Assert.ThrowsAsync<BusinessException>(() => service.ExportAsync(new() { From = f.Request.From, To = f.Request.To }))).ErrorCode);
        Assert.False(await f.Db.OperationLogs.AsNoTracking().AnyAsync(a => a.Module == "AiTechnicalExport" && a.Action == "Prepared"));
        Assert.True(await f.Db.OperationLogs.AsNoTracking().AnyAsync(a => a.Module == "AiTechnicalExport" && a.Action == "Requested"));
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task Export_PreparedPersistenceFailureRetainsAttemptWithoutDeliveringFile()
    {
        await using var f = await SqlFixture.CreateAsync(); await GrantAsync(f);
        var work = new FailPreparedWork(new UnitOfWork(f.Db));
        await Assert.ThrowsAsync<IOException>(() => Service(f, work: work).ExportAsync(new() { From = f.Request.From, To = f.Request.To }));
        Assert.Equal(1, await f.Db.OperationLogs.AsNoTracking().CountAsync(a => a.Module == "AiTechnicalExport" && a.Action == "Requested"));
        Assert.False(await f.Db.OperationLogs.AsNoTracking().AnyAsync(a => a.Module == "AiTechnicalExport" && a.Action == "Prepared"));
    }

    internal static async Task GrantAsync(SqlFixture f)
    {
        var permission = new Permission { Id = Guid.NewGuid(), TenantId = f.Actor.TenantId,
            Code = AiCenterConstants.OperationsExportPermission, Name = "Synthetic technical export" };
        f.Db.Permissions.Add(permission);
        f.Db.RolePermissions.Add(new() { TenantId = f.Actor.TenantId, RoleId = f.Role.Id, PermissionId = permission.Id });
        await f.Db.SaveChangesAsync();
    }
    internal static AiTechnicalExportService Service(SqlFixture f, IAsyncQueryExecutor? executor = null, IUnitOfWork? work = null) => new(
        new Repository<AiRun>(f.Db), new Repository<AiUsageLog>(f.Db), new Repository<OperationLog>(f.Db),
        executor ?? new EfCoreAsyncQueryExecutor(), work ?? new UnitOfWork(f.Db),
        f.Tenant, new(new SqlCurrentUser(f.Actor, f.Role, export: true), f.Tenant,
            new UserCredentialValidator(f.Db, new PasswordHasher<User>(), f.Tenant)), new AllowedRates(),
        new MemoryDistributedLock(new LockOptions()), TimeProvider.System, NullLogger<AiTechnicalExportService>.Instance);
    private sealed class AllowedRates : IDistributedRateLimitService
    {
        public Task<RateLimitAcquireResult> TryAcquireAsync(string policyName, string partitionKey, int permitLimit, TimeSpan window, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(RateLimitAcquireResult.Acquired); }
    }
    private sealed class FailPreparedWork(IUnitOfWork inner) : IUnitOfWork
    {
        private int _count;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => ++_count == 1 ? inner.SaveChangesAsync(ct) : throw new IOException("Synthetic audit failure");
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default) => inner.ExecuteInTransactionAsync(action, ct);
    }
}
