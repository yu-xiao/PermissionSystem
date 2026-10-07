using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Repositories;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic012TechnicalExportReceiptSqlTranslationTests
{
    [Fact]
    public async Task Query_TranslatesBoundedOwnTenantDedicatedProjectionWithoutConnecting()
    {
        var f = new ReceiptFixture(); f.Add(Guid.NewGuid(), "Prepared");
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=DesignOnly;Integrated Security=True;Connect Timeout=1").Options, f.Export.Tenant, new NullAuditContext());
        var executor = new CaptureExecutor(f.Export.Logs.Query());
        Assert.Equal(1, (await f.Service(executor, new Repository<OperationLog>(db)).QueryAsync(f.Request)).MatchedRecordCount);
        Assert.Equal(2, executor.Sql.Count);
        foreach (var sql in executor.Sql)
        {
            foreach (var required in new[] { "1001", "[TenantId]", "[UserId]", "[IsDeleted]", "[CreatedAt] >=", "[CreatedAt] <", "[Module]", "[Method]", "[RequestPath]", "ORDER BY", "[RequestBody]" }) Assert.Contains(required, sql);
            foreach (var excluded in new[] { "[ResponseBody]", "[UserName]", "[IpAddress]", "[UserAgent]", "[TraceId]" }) Assert.DoesNotContain(excluded, sql);
        }
    }
    private sealed class CaptureExecutor(IQueryable<OperationLog> source) : IAsyncQueryExecutor
    {
        public List<string> Sql { get; } = [];
        public Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); Sql.Add(query.ToQueryString());
            return Task.FromResult<IReadOnlyList<T>>(new EnumerableQuery<T>(new Roots(source).Visit(query.Expression)!).ToArray());
        }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Roots(IQueryable<OperationLog> source) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression ? source.Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) => node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) &&
            node.Method.Name is nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters) or nameof(EntityFrameworkQueryableExtensions.AsNoTracking) ? Visit(node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
