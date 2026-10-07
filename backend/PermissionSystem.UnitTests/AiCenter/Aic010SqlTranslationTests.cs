using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Infrastructure.Ai;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic010SqlTranslationTests
{
    [Fact]
    public async Task Search_TranslatesTenantAclValidityAndBoundedLiteralMatchIntoSql()
    {
        using var f = new Aic010TestFixture();
        using var sql = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=DesignOnly;Integrated Security=True;Connect Timeout=1").Options,
            f.Tenant, new NullAuditContext());
        Repository<T> Repo<T>() where T : BaseEntity => new(sql);
        var capture = new CaptureExecutor();
        var access = new AiKnowledgeAccessPolicy(f.Current, f.Tenant, f.Identities, f.Options, f.Options, capture,
            Repo<AiKnowledgeDocument>(), Repo<AiKnowledgeDocumentVersion>(), Repo<AiKnowledgeDocumentRole>(),
            Repo<Role>(), Repo<UserRole>(), Repo<FileResource>());
        var service = new AiKnowledgeService(access, Repo<AiKnowledgeDocument>(), Repo<AiKnowledgeDocumentVersion>(),
            Repo<AiKnowledgeChunk>(), Repo<AiKnowledgeDocumentRole>(), Repo<Role>(), Repo<FileResource>(),
            Repo<AiKnowledgeRunReference>(), Repo<AiRun>(), Repo<AiMessage>(), Repo<AiToolInvocation>(), f.Files,
            new AiKnowledgeTextParser(), capture, f.Unit, new TestSupport.TestDistributedLock());
        await service.SearchAsync(new() { Keyword = "请假%_", Limit = 5 });
        var query = capture.Sql!;
        Assert.Contains("TOP(", query); Assert.Contains("LIKE", query); Assert.Contains("EXISTS", query);
        Assert.Contains("[ai_knowledge_document_role]", query); Assert.Contains("[UserRoles]", query);
        Assert.Contains("[Roles]", query); Assert.Contains("[FileResources]", query);
        Assert.Contains("[ValidFrom]", query); Assert.Contains("[ValidUntil]", query);
        Assert.Contains("[CurrentVersionId]", query); Assert.Contains("[TenantId]", query);
        Assert.Contains("[ScanStatus]", query); Assert.Contains("ORDER BY", query);
        Assert.Contains("\\%\\_", query);
    }

    [Fact]
    public void Model_UsesTenantCompositeForeignKeysAndRowVersion()
    {
        using var f = new Aic010TestFixture();
        var types = new[] { typeof(AiKnowledgeDocument), typeof(AiKnowledgeDocumentVersion), typeof(AiKnowledgeChunk),
            typeof(AiKnowledgeDocumentRole), typeof(AiKnowledgeRunReference) };
        foreach (var type in types)
        {
            var entity = f.Db.Model.FindEntityType(type)!;
            Assert.True(entity.FindProperty("RowVersion")!.IsConcurrencyToken);
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal("TenantId", fk.Properties[0].Name));
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        }
    }

    [Fact]
    public async Task CommitFence_RejectsNonSqlAndMissingTransaction()
    {
        using var f = new Aic010TestFixture();
        var fence = new AiKnowledgeCommitFence(f.Db, f.Current);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fence.HoldAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fence.HoldDocumentAsync(Guid.NewGuid(), default));
    }

    private sealed class CaptureExecutor : IAsyncQueryExecutor
    {
        public string? Sql { get; private set; }
        public Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        { Sql = query.ToQueryString(); return Task.FromResult<IReadOnlyList<T>>([]); }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
