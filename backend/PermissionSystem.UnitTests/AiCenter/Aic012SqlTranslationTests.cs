using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Repositories;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic012SqlTranslationTests
{
    [Fact]
    public async Task Query_TranslatesBoundedProjectionsAndTenantSafeAssociationsWithoutOpeningSql()
    {
        var f = new Aic012Fixture(); var scenario = f.AddScenario(); var run = f.AddRun(AiRunStatus.Completed, scenario.Id, 20);
        f.AddUsage(run, AiInvocationStatus.Completed, 1, 2, 0.1m, "USD"); f.AddFeedback(run, AiFeedbackRating.Positive);
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=DesignOnly;Integrated Security=True;Connect Timeout=1").Options,
            f.Tenant, new NullAuditContext());
        Repository<T> Repo<T>() where T : BaseEntity => new(db);
        var capture = new CaptureExecutor(new Dictionary<Type, IQueryable>
        {
            [typeof(AiRun)] = f.Runs.Query(), [typeof(AiUsageLog)] = f.Usages.Query(),
            [typeof(AiUserFeedback)] = f.Feedback.Query(), [typeof(AiScenario)] = f.Scenarios.Query()
        });
        var service = new AiScenarioOperationsService(Repo<AiRun>(), Repo<AiUsageLog>(), Repo<AiUserFeedback>(), Repo<AiScenario>(),
            capture, f.Current, f.Tenant, f.Identities);
        var result = await service.QueryAsync(f.Request);
        Assert.Equal(1, Assert.Single(result.Scenarios.Items).InputTokens);
        Assert.Equal(4, capture.ListSql.Count);
        var population = capture.ListSql[0]; var usage = capture.ListSql[1]; var feedback = capture.ListSql[2];
        foreach (var sql in capture.ListSql)
        {
            Assert.Contains("[TenantId]", sql); Assert.Contains("[IsDeleted]", sql);
            Assert.DoesNotContain("[Content]", sql); Assert.DoesNotContain("[Comment]", sql);
            Assert.DoesNotContain("[ExecutionConfigurationJson]", sql); Assert.DoesNotContain("[SnapshotJson]", sql);
        }
        Assert.Contains("TOP(", population); Assert.Contains("10001", population);
        Assert.Contains("[CreatedAt] >=", population); Assert.Contains("[CreatedAt] <", population);
        Assert.Contains("INNER JOIN", usage); Assert.Contains("50001", usage);
        Assert.Contains("INNER JOIN", feedback); Assert.Contains("10001", feedback);
        Assert.Contains("[UserId]", feedback); Assert.Contains("[ActorUserId]", feedback);
        Assert.Contains("[MessageId]", feedback); Assert.Contains("[ResponseMessageId]", feedback);
        Assert.DoesNotContain("[EstimatedInputTokens]", usage); Assert.DoesNotContain("[ReservedCost]", usage);
        Assert.All(capture.CountSql, sql => Assert.Contains("[TenantId]", sql));
    }

    private sealed class CaptureExecutor(Dictionary<Type, IQueryable> data) : IAsyncQueryExecutor
    {
        public List<string> ListSql { get; } = [];
        public List<string> CountSql { get; } = [];
        public Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); ListSql.Add(query.ToQueryString());
            return Task.FromResult<IReadOnlyList<T>>(Evaluate(query).ToArray());
        }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); CountSql.Add(query.ToQueryString()); return Task.FromResult(Evaluate(query).LongCount()); }
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) => throw new NotSupportedException();
        private IQueryable<T> Evaluate<T>(IQueryable<T> query) => new EnumerableQuery<T>(new SyntheticRoots(data).Visit(query.Expression)!);
    }

    // Evaluate the same translated expression against synthetic roots, without a database connection.
    private sealed class SyntheticRoots(Dictionary<Type, IQueryable> data) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression root
            ? data[root.EntityType.ClrType].Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) =>
            node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) &&
            node.Method.Name is nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters) or nameof(EntityFrameworkQueryableExtensions.AsNoTracking)
                ? Visit(node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
