using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Repositories;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic012CostQualitySqlTranslationTests
{
    [Fact]
    public async Task Query_TranslatesTwoBoundedTenantSafeScalarReadsWithoutConnecting()
    {
        var f = new CostQualityFixture(); f.Add();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=DesignOnly;Integrated Security=True;Connect Timeout=1").Options, f.Tenant, new NullAuditContext());
        var executor = new CaptureExecutor(new Dictionary<Type, IQueryable> { [typeof(AiRun)] = f.Core.Runs.Query(), [typeof(AiUsageLog)] = f.Core.Usages.Query() });
        Assert.Equal(1, (await f.Service(executor, new Repository<AiRun>(db), new Repository<AiUsageLog>(db)).QueryAsync(f.Request)).Population.InvocationCount);
        Assert.Equal(2, executor.Sql.Count);
        foreach (var sql in executor.Sql)
        {
            foreach (var column in new[] { "50001", "INNER JOIN", "[TenantId]", "[IsDeleted]", "[CreatedAt] >=", "[CreatedAt] <", "ORDER BY", "[InputTokenPricePerMillion]", "[EstimatedInputTokens]", "[TotalTokens]" }) Assert.Contains(column, sql);
            foreach (var column in new[] { "[ModelName]", "[ProviderRequestId]", "[ActorUserId]", "[TraceId]", "[ErrorCode]", "[ReservedCost]", "[ExecutionConfigurationJson]", "[ApiKeyEncrypted]" }) Assert.DoesNotContain(column, sql);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(sql, @"\[TenantId\] =").Count);
        }
    }
    internal sealed class CaptureExecutor(Dictionary<Type, IQueryable> data) : IAsyncQueryExecutor
    {
        public List<string> Sql { get; } = [];
        public Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Sql.Add(query.ToQueryString()); return Task.FromResult<IReadOnlyList<T>>(new EnumerableQuery<T>(new Roots(data).Visit(query.Expression)!).ToArray()); }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Roots(Dictionary<Type, IQueryable> data) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression root ? data[root.EntityType.ClrType].Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) => node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) &&
            node.Method.Name is nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters) or nameof(EntityFrameworkQueryableExtensions.AsNoTracking) ? Visit(node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
