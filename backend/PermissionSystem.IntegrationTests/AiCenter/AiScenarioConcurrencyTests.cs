using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.UnitOfWork;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class AiScenarioConcurrencyTests
{
    private const string ConnectionEnvironment = "PERMISSION_SYSTEM_SQLSERVER_TEST_CONNECTION";

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ConcurrentPointerChanges_ShouldCommitOnlyOneEventAndVersionPointer()
    {
        var tenantId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        try
        {
            await using (var seed = Context(tenantId))
            {
                Assert.Empty(await seed.Database.GetPendingMigrationsAsync());
                seed.AiScenarios.Add(new() { Id = scenarioId, TenantId = tenantId, Code = "synthetic-concurrency", Name = "Synthetic SQL constraint test" });
                await seed.SaveChangesAsync();
                seed.AiScenarioVersions.AddRange(Version(tenantId, scenarioId, first, 1), Version(tenantId, scenarioId, second, 2));
                await seed.SaveChangesAsync();
            }
            await using var a = Context(tenantId); await using var b = Context(tenantId);
            var scenarioA = await a.AiScenarios.SingleAsync(s => s.Id == scenarioId);
            var scenarioB = await b.AiScenarios.SingleAsync(s => s.Id == scenarioId);
            Assert.Equal(scenarioA.RowVersion, scenarioB.RowVersion);
            var results = await Task.WhenAll(Change(a, scenarioA, first), Change(b, scenarioB, second));
            Assert.Single(results, r => r);
            await using var verify = Context(tenantId);
            var saved = await verify.AiScenarios.SingleAsync(s => s.Id == scenarioId);
            var fact = await verify.AiScenarioReleaseEvents.SingleAsync(e => e.ScenarioId == scenarioId);
            Assert.Equal(saved.CurrentVersionId, fact.VersionId);
            Assert.Equal(1, saved.Revision);
        }
        finally { await Cleanup(tenantId); }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task CompositeForeignKey_ShouldRejectCrossTenantVersionReference()
    {
        var tenantId = Guid.NewGuid(); var otherTenant = Guid.NewGuid(); var scenarioId = Guid.NewGuid();
        try
        {
            await using var seed = Context(tenantId); Assert.Empty(await seed.Database.GetPendingMigrationsAsync());
            seed.AiScenarios.Add(new() { Id = scenarioId, TenantId = tenantId, Code = "synthetic-tenant", Name = "Synthetic SQL constraint test" });
            await seed.SaveChangesAsync();
            await using var other = Context(otherTenant);
            other.AiScenarioVersions.Add(Version(otherTenant, scenarioId, Guid.NewGuid(), 1));
            await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        }
        finally { await Cleanup(otherTenant); await Cleanup(tenantId); }
    }

    private static AiScenarioVersion Version(Guid tenant, Guid scenario, Guid id, int number) => new()
    {
        Id = id, TenantId = tenant, ScenarioId = scenario, VersionNumber = number,
        SnapshotJson = "{}", ContentHash = new string('a', 64), BuildIdentity = new string('b', 64)
    };
    private static async Task<bool> Change(AppDbContext db, AiScenario scenario, Guid version)
    {
        try
        {
            await new UnitOfWork(db).ExecuteInTransactionAsync(async ct =>
            {
                scenario.CurrentVersionId = version; scenario.Revision++;
                db.AiScenarioReleaseEvents.Add(new() { TenantId = scenario.TenantId, ScenarioId = scenario.Id,
                    VersionId = version, Type = AiScenarioEventType.Published, Sequence = scenario.Revision,
                    ActorUserId = Guid.NewGuid(), Reason = "Synthetic atomicity test; no application publication" });
                await db.SaveChangesAsync(ct);
            });
            return true;
        }
        catch (BusinessException) { return false; }
    }
    private static AppDbContext Context(Guid tenantId)
    {
        var tenant = new TenantContext(); tenant.SetTenant(tenantId, "test");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Environment.GetEnvironmentVariable(ConnectionEnvironment)!).Options,
            tenant, new NullAuditContext());
    }
    private static async Task Cleanup(Guid tenantId)
    {
        await using var db = Context(tenantId);
        // Cleanup is limited to fresh synthetic tenants owned by this isolated SQL test.
        await db.AiScenarioReleaseEvents.Where(e => e.TenantId == tenantId).ExecuteDeleteAsync();
        await db.AiScenarioEvaluations.Where(e => e.TenantId == tenantId).ExecuteDeleteAsync();
        await db.AiScenarioDrafts.Where(e => e.TenantId == tenantId).ExecuteDeleteAsync();
        await db.AiScenarios.Where(e => e.TenantId == tenantId).ExecuteUpdateAsync(set => set.SetProperty(e => e.CurrentVersionId, (Guid?)null));
        await db.AiScenarioVersions.Where(e => e.TenantId == tenantId).ExecuteDeleteAsync();
        await db.AiScenarios.Where(e => e.TenantId == tenantId).ExecuteDeleteAsync();
    }
    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnvironment))) Skip = "Isolated SQL Server connection and reviewed migration are required."; }
    }
}
