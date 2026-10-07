using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic010KnowledgeSqlTests
{
    private const string ConnectionVariable = "PERMISSION_SYSTEM_AIC010_SQL_TEST_CONNECTION";
    private const string IsolatedVariable = "PERMISSION_SYSTEM_AIC010_SQL_TEST_ISOLATED";

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task CompositeForeignKey_RejectsCrossTenantGrant()
    {
        await using var db = Context(Guid.NewGuid());
        await RequireReviewedSchema(db);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var seed = await Seed(db);
        var otherTenant = new TenantContext(); otherTenant.SetTenant(Guid.NewGuid(), "Synthetic SQL fixture");
        await using var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(db.Database.GetDbConnection()).Options, otherTenant, new NullAuditContext());
        await other.Database.UseTransactionAsync(transaction.GetDbTransaction());
        other.AiKnowledgeDocumentRoles.Add(new() { TenantId = otherTenant.TenantId!.Value,
            DocumentId = seed.Document.Id, RoleId = seed.RoleId });
        await Assert.ThrowsAsync<BusinessException>(() => other.SaveChangesAsync());
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task UniqueVersion_RejectsDuplicateDocumentVersionNumber()
    {
        await using var db = Context(Guid.NewGuid()); await RequireReviewedSchema(db);
        await using var transaction = await db.Database.BeginTransactionAsync(); var seed = await Seed(db);
        db.AiKnowledgeDocumentVersions.Add(new() { TenantId = db.CurrentTenantId!.Value, DocumentId = seed.Document.Id,
            VersionNumber = 1, ContentHash = new string('1', 64), ValidFrom = DateTimeOffset.UtcNow, ValidUntil = DateTimeOffset.UtcNow.AddHours(1) });
        await Assert.ThrowsAsync<BusinessException>(() => db.SaveChangesAsync());
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task RowVersion_RejectsStalePublicationMetadataWrite()
    {
        await using var db = Context(Guid.NewGuid()); await RequireReviewedSchema(db);
        await using var transaction = await db.Database.BeginTransactionAsync(); var seed = await Seed(db);
        Assert.NotEmpty(seed.Document.RowVersion);
        await db.AiKnowledgeDocuments.Where(d => d.Id == seed.Document.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.AccessVersion, d => d.AccessVersion + 1));
        seed.Document.Title = "Stale synthetic write";
        await Assert.ThrowsAsync<BusinessException>(() => db.SaveChangesAsync());
    }

    [SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task ExpiryCleanup_ClearsBodySchedulesFileAndRetainsReferenceKeys()
    {
        await using var db = Context(Guid.NewGuid()); await RequireReviewedSchema(db);
        await using var transaction = await db.Database.BeginTransactionAsync(); var seed = await Seed(db);
        await AiKnowledgeRetention.CleanupExpiredAsync(db, DateTimeOffset.UtcNow.AddDays(2), default);
        db.ChangeTracker.Clear();
        Assert.Equal("", (await db.AiKnowledgeChunks.SingleAsync()).Content);
        Assert.Equal(FileStatus.PendingDelete, (await db.FileResources.SingleAsync()).FileStatus);
        Assert.Equal(seed.Document.Id, (await db.AiKnowledgeDocumentVersions.SingleAsync()).DocumentId);
        Assert.Equal(seed.Hash, (await db.AiKnowledgeChunks.SingleAsync()).ContentHash);
    }

    private static AppDbContext Context(Guid tenantId)
    {
        var tenant = new TenantContext(); tenant.SetTenant(tenantId, "Synthetic SQL fixture");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(
            Environment.GetEnvironmentVariable(ConnectionVariable)!).Options, tenant, new NullAuditContext());
    }
    private static async Task RequireReviewedSchema(AppDbContext db) =>
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

    private static async Task<Seeded> Seed(AppDbContext db)
    {
        var tenantId = db.CurrentTenantId!.Value;
        var document = new AiKnowledgeDocument { Id = Guid.NewGuid(), TenantId = tenantId, Title = "合成 SQL 制度",
            Owner = "Synthetic Owner", License = "Synthetic fixtures", LastVersionNumber = 1 };
        var role = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Code = "synthetic", Name = "Synthetic" };
        var file = new FileResource { Id = Guid.NewGuid(), TenantId = tenantId, OriginalName = "synthetic.txt",
            FileName = "synthetic.txt", Extension = ".txt", BusinessType = AiKnowledgeContract.BusinessType,
            BusinessId = document.Id, ObjectKey = Guid.NewGuid().ToString("N"), FileStatus = FileStatus.Active, ScanStatus = FileScanStatus.Clean };
        var version = new AiKnowledgeDocumentVersion { Id = Guid.NewGuid(), TenantId = tenantId, DocumentId = document.Id,
            VersionNumber = 1, FileResourceId = file.Id, ParseStatus = AiKnowledgeParseStatus.Ready, ContentHash = new string('A', 64),
            ValidFrom = DateTimeOffset.UtcNow.AddHours(-1), ValidUntil = DateTimeOffset.UtcNow.AddDays(1), PublishedAt = DateTimeOffset.UtcNow };
        const string content = "合成请假需要确认。";
        var hash = AiStructuredResults.Digest(content);
        db.Tenants.Add(new() { Id = tenantId, TenantId = tenantId, Code = $"aic010-{tenantId:N}", Name = "Synthetic SQL fixture" });
        db.Roles.Add(role); db.AiKnowledgeDocuments.Add(document); db.FileResources.Add(file);
        db.AiKnowledgeDocumentVersions.Add(version);
        db.AiKnowledgeDocumentRoles.Add(new() { TenantId = tenantId, DocumentId = document.Id, RoleId = role.Id });
        db.AiKnowledgeChunks.Add(new() { TenantId = tenantId, DocumentId = document.Id, VersionId = version.Id,
            Sequence = 1, StartLine = 1, EndLine = 1, Content = content, ContentHash = hash });
        await db.SaveChangesAsync(); document.CurrentVersionId = version.Id; await db.SaveChangesAsync();
        return new(document, role.Id, hash);
    }
    private sealed record Seeded(AiKnowledgeDocument Document, Guid RoleId, string Hash);
    private sealed class SqlFactAttribute : FactAttribute
    {
        public SqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)) ||
                Environment.GetEnvironmentVariable(IsolatedVariable) != "1")
                Skip = "Requires explicitly isolated AIC-010 SQL Server with manually reviewed/applied migrations. Tests rollback their own synthetic fixtures.";
        }
    }
}
