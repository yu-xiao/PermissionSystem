using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Repositories;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic012TechnicalExportSqlTranslationTests
{
    [Fact]
    public async Task Export_TranslatesBoundedScalarWhitelistAndTenantSafeAssociationWithoutOpeningSql()
    {
        var f = new TechnicalExportFixture(); var run = f.Core.AddRun(AiRunStatus.Completed);
        f.Core.AddUsage(run, AiInvocationStatus.Completed, 1, 2, 1, "USD");
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=DesignOnly;Integrated Security=True;Connect Timeout=1").Options, f.Tenant, new NullAuditContext());
        var executor = new CaptureExecutor(new Dictionary<Type, IQueryable>
        { [typeof(AiRun)] = f.Core.Runs.Query(), [typeof(AiUsageLog)] = f.Core.Usages.Query() });
        await f.Service(executor, new Repository<AiRun>(db), new Repository<AiUsageLog>(db)).ExportAsync(f.Request);
        Assert.Equal(4, executor.Sql.Count);
        Assert.Contains("10001", executor.Sql[0]); Assert.Contains("50001", executor.Sql[1]);
        Assert.Contains("[CreatedAt] >=", executor.Sql[0]); Assert.Contains("[CreatedAt] <", executor.Sql[0]);
        Assert.Contains("ORDER BY", executor.Sql[0]); Assert.Contains("INNER JOIN", executor.Sql[1]);
        foreach (var sql in executor.Sql)
        {
            Assert.Contains("[TenantId]", sql); Assert.Contains("[IsDeleted]", sql);
            foreach (var column in new[] { "ActorUserId", "ActorSessionId", "ActorSecurityStamp", "ConversationId", "RequestMessageId", "ResponseMessageId", "TraceId", "ErrorSummary", "ExecutionConfigurationJson", "ModelName", "ProviderRequestId", "ReservedCost", "Content" })
                Assert.DoesNotContain($"[{column}]", sql);
        }
        Assert.Equal(2, f.Work.Persisted.Count);
    }

    private sealed class CaptureExecutor(Dictionary<Type, IQueryable> roots) : IAsyncQueryExecutor
    {
        public List<string> Sql { get; } = [];
        public Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Sql.Add(query.ToQueryString()); return Task.FromResult<IReadOnlyList<T>>(Evaluate(query).ToArray()); }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Assert.Contains("[TenantId]", query.ToQueryString()); return Task.FromResult(Evaluate(query).LongCount()); }
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) => throw new NotSupportedException();
        private IQueryable<T> Evaluate<T>(IQueryable<T> query) => new EnumerableQuery<T>(new SyntheticRoots(roots).Visit(query.Expression)!);
    }
    private sealed class SyntheticRoots(Dictionary<Type, IQueryable> roots) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression root
            ? roots[root.EntityType.ClrType].Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) => node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) &&
            node.Method.Name is nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters) or nameof(EntityFrameworkQueryableExtensions.AsNoTracking)
                ? Visit(node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
