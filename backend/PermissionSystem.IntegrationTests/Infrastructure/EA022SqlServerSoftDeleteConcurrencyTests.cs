using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.IntegrationTests.Infrastructure;

public sealed class EA022SqlServerSoftDeleteConcurrencyTests
{
    private const string ConnectionEnvName = "PERMISSION_SYSTEM_SQLSERVER_TEST_CONNECTION";

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task SoftDeletedBusinessKey_ShouldBeReusableAcrossRepeatedLifecycles()
    {
        var tenantId = Guid.NewGuid();
        var code = $"EA022-{Guid.NewGuid():N}";

        await using (var setup = CreateContext(tenantId))
        {
            await setup.Database.MigrateAsync();
            setup.Tenants.Add(CreateTenant(tenantId));
            await setup.SaveChangesAsync();
        }

        try
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                await using var context = CreateContext(tenantId);
                var department = new Department
                {
                    TenantId = tenantId,
                    Code = code,
                    Name = $"EA-022 lifecycle {cycle}",
                    TreePath = "/",
                    Status = "Enabled",
                    IsEnabled = true
                };
                context.Departments.Add(department);
                await context.SaveChangesAsync();

                if (cycle < 2)
                {
                    context.Departments.Remove(department);
                    await context.SaveChangesAsync();
                }
            }

            await using var verification = CreateContext(tenantId);
            Assert.Equal(1, await verification.Departments.CountAsync(entity => entity.Code == code));
            Assert.Equal(3, await verification.Departments.IgnoreQueryFilters()
                .CountAsync(entity => entity.TenantId == tenantId && entity.Code == code));
        }
        finally
        {
            await CleanupAsync(tenantId, code);
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task StaleBaseEntityUpdate_ShouldReturnConflict()
    {
        var tenantId = Guid.NewGuid();
        var code = $"EA022-{Guid.NewGuid():N}";

        await using (var setup = CreateContext(tenantId))
        {
            await setup.Database.MigrateAsync();
            setup.Tenants.Add(CreateTenant(tenantId));
            setup.Departments.Add(new Department
            {
                TenantId = tenantId,
                Code = code,
                Name = "EA-022 concurrency",
                TreePath = "/",
                Status = "Enabled",
                IsEnabled = true
            });
            await setup.SaveChangesAsync();
        }

        try
        {
            await using var firstContext = CreateContext(tenantId);
            await using var secondContext = CreateContext(tenantId);
            var first = await firstContext.Departments.SingleAsync(entity => entity.Code == code);
            var stale = await secondContext.Departments.SingleAsync(entity => entity.Code == code);

            first.Name = "EA-022 first writer";
            await firstContext.SaveChangesAsync();

            stale.Name = "EA-022 stale writer";
            var exception = await Assert.ThrowsAsync<BusinessException>(() => secondContext.SaveChangesAsync());

            Assert.Equal(ErrorCode.Conflict, exception.ErrorCode);
        }
        finally
        {
            await CleanupAsync(tenantId, code);
        }
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable(ConnectionEnvName)!)
            .Options;
        return new AppDbContext(options, new TestTenantContext(tenantId), new NullAuditContext());
    }

    private static async Task CleanupAsync(Guid tenantId, string code)
    {
        await using var cleanup = CreateContext(tenantId);
        await cleanup.Departments.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == tenantId && entity.Code == code)
            .ExecuteDeleteAsync();
        await cleanup.Tenants.IgnoreQueryFilters()
            .Where(entity => entity.Id == tenantId)
            .ExecuteDeleteAsync();
    }

    private static Tenant CreateTenant(Guid tenantId) => new()
    {
        Id = tenantId,
        TenantId = tenantId,
        Code = $"ea022-{tenantId:N}",
        Name = "EA-022 test tenant",
        Status = TenantStatus.Active,
        StatusChangedAt = DateTimeOffset.UtcNow,
        InitializationStep = "Completed",
        InitializationProgress = 100,
        InitializedAt = DateTimeOffset.UtcNow
    };

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnvName)))
            {
                Skip = $"Set {ConnectionEnvName} to run SQL Server integration tests.";
            }
        }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
        }

        public Guid? TenantId { get; }

        public string? Source => "Test";

        public bool IsResolved => true;

        public bool IsSuperAdmin => false;

        public bool IsSystemScopeActive => false;

        public bool IsHttpRequest => false;

        public void SetTenant(Guid tenantId, string source)
        {
        }

        public void MarkAsSuperAdmin(bool isSuperAdmin)
        {
        }

        public void MarkAsHttpRequest()
        {
        }
    }
}
