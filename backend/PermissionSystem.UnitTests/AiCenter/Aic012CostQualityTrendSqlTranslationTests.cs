using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Repositories;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic012CostQualityTrendSqlTranslationTests
{
    [Fact]
    public async Task Trend_UsesExactlyTwoBoundedScalarQueriesWithoutDatabaseDateGroupingOrNPlusOne()
    {
        var f = new CostQualityFixture(); f.Add();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=DesignOnly;Integrated Security=True;Connect Timeout=1").Options, f.Tenant, new NullAuditContext());
        var executor = new Aic012CostQualitySqlTranslationTests.CaptureExecutor(new Dictionary<Type, IQueryable>
            { [typeof(AiRun)] = f.Core.Runs.Query(), [typeof(AiUsageLog)] = f.Core.Usages.Query() });
        var result = await f.Service(executor, new Repository<AiRun>(db), new Repository<AiUsageLog>(db))
            .QueryTrendAsync(new() { From = f.Core.To.AddDays(-90).AddHours(-1), To = f.Core.To.AddHours(-1) });
        Assert.Equal(91, result.Daily.Count); Assert.Equal(2, executor.Sql.Count);
        foreach (var sql in executor.Sql)
        {
            foreach (var column in new[] { "50001", "INNER JOIN", "[IsDeleted]", "[CreatedAt] >=", "[CreatedAt] <", "ORDER BY", "[EstimatedCost]", "[InputTokenPricePerMillion]" }) Assert.Contains(column, sql);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(sql, @"\[TenantId\] =").Count);
            foreach (var column in new[] { "GROUP BY", "DATEPART", "[ModelName]", "[ProviderConfigId]", "[ProviderRequestId]", "[ActorUserId]", "[TraceId]", "[ReservedCost]", "[ExecutionConfigurationJson]" }) Assert.DoesNotContain(column, sql);
        }
    }
}
