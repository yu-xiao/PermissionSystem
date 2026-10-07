using System.Text;
using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Files;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Infrastructure.Queries;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

internal sealed class Aic010TestFixture : IDisposable
{
    public static readonly string[] Permissions = [AiCenterConstants.KnowledgeViewPermission, AiCenterConstants.KnowledgeManagePermission,
        AiCenterConstants.KnowledgeQueryPermission, AiCenterConstants.ChatUsePermission, AiCenterConstants.ToolQueryPermission,
        AiCenterConstants.ConversationViewPermission];
    public TenantContext Tenant { get; } = new();
    public TestCurrentUserService Current { get; }
    public AiQueryTestFixture.IdentitySource Identities { get; } = new();
    public AppDbContext Db { get; }
    public Guid RoleId { get; } = Guid.NewGuid();
    public AiKnowledgeAccessPolicy Access { get; }
    public AiKnowledgeService Service { get; }
    public AiKnowledgeRunGuard Guard { get; }
    public FileService Files { get; }
    public MemoryStorage Storage { get; } = new();
    public AiCenterOptions Options { get; }
    public EfCoreAsyncQueryExecutor Queries { get; } = new();
    public IUnitOfWork Unit { get; }
    public AiQueryAccessGuard QueryGuard { get; }

    public Aic010TestFixture(string[]? permissions = null, bool superAdmin = false, bool enabled = true)
    {
        var codes = permissions ?? Permissions;
        Current = new(permissions: codes, isSuperAdmin: superAdmin);
        Identities.Actor = Identities.Actor with { PermissionCodes = codes,
            Roles = superAdmin ? [ClaimConstants.SuperAdminRoleCode] : ["reader"] };
        Tenant.SetTenant(TestIds.TenantId, "synthetic");
        Options = new() { Enabled = true, EnableKnowledgeDocumentTool = enabled, AllowedTenantIds = [TestIds.TenantId] };
        Db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Tenant, new NullAuditContext());
        Unit = new MemoryUnit(Db);
        Access = new(Current, Tenant, Identities, Options, Options, Queries, Repo<AiKnowledgeDocument>(), Repo<AiKnowledgeDocumentVersion>(),
            Repo<AiKnowledgeDocumentRole>(), Repo<Role>(), Repo<UserRole>(), Repo<FileResource>());
        var demo = new DataPermissionRepository<DemoBusinessOrder>(Repo<DemoBusinessOrder>(), new AiQueryTestFixture.ScopeSource(),
            new DataPermissionFilter(), new DemoBusinessOrderDataPermissionSpecification());
        Files = new(Repo<FileResource>(), Unit, Storage, new FileContentScanner(), new FileBusinessAccessChecker(demo, Access),
            Current, new TestTenantWriteResolver(), new FileStorageOptions());
        Service = new(Access, Repo<AiKnowledgeDocument>(), Repo<AiKnowledgeDocumentVersion>(), Repo<AiKnowledgeChunk>(), Repo<AiKnowledgeDocumentRole>(),
            Repo<Role>(), Repo<FileResource>(), Repo<AiKnowledgeRunReference>(), Repo<AiRun>(), Repo<AiMessage>(), Repo<AiToolInvocation>(),
            Files, new AiKnowledgeTextParser(), Queries, Unit, new TestDistributedLock());
        Guard = new(Service, Access, Repo<AiKnowledgeRunReference>(), Repo<AiRun>(), Repo<AiToolInvocation>(), Queries, Current, messages: Repo<AiMessage>());
        QueryGuard = new(Current, Tenant, Identities, Options, new AiQueryTestFixture.ScopeSource());
        Db.Roles.Add(new() { Id = RoleId, TenantId = TestIds.TenantId, Code = "reader", Name = "Synthetic reader", IsEnabled = true });
        Db.UserRoles.Add(new() { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, UserId = TestIds.NormalUserId, RoleId = RoleId });
        Db.SaveChanges();
    }

    public Repository<T> Repo<T>() where T : PermissionSystem.Domain.Common.BaseEntity => new(Db);

    public Task<AiKnowledgeDocumentResponse> Create(string title = "合成请假制度", Guid[]? roleIds = null) => Service.CreateAsync(new()
        { Title = title, Owner = "Synthetic Owner", License = "Synthetic fixtures", Synthetic = true, RoleIds = roleIds ?? [RoleId] });

    public async Task<AiKnowledgeDocumentResponse> Import(AiKnowledgeDocumentResponse doc, string text = "请假申请需要负责人确认。", DateTimeOffset? until = null)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var stream = new MemoryStream(bytes);
        return await Service.UploadAsync(doc.Id, new() { RowVersion = doc.RowVersion, File = new UploadFileRequest { Content = stream,
            OriginalName = "synthetic.txt", ContentType = "text/plain", Size = bytes.Length },
            ValidFrom = DateTimeOffset.UtcNow.AddMinutes(-5), ValidUntil = until ?? DateTimeOffset.UtcNow.AddDays(1) });
    }

    public async Task<AiKnowledgeDocumentResponse> Published(string text = "请假申请需要负责人确认。", string title = "合成请假制度", Guid[]? roleIds = null)
    {
        var doc = await Import(await Create(title, roleIds), text);
        await Service.PublishAsync(doc.Id, doc.Versions[0].Id, new() { RowVersion = doc.RowVersion });
        return (await Service.ListAsync(1, 50)).Items.Single(d => d.Id == doc.Id);
    }

    public void Dispose() => Db.Dispose();

    private sealed class MemoryUnit(AppDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) => action(cancellationToken);
    }

    internal sealed class MemoryStorage : IFileStorageService
    {
        public bool FailSave { get; set; }
        private readonly Dictionary<string, byte[]> _content = [];
        public string StorageProvider => "SyntheticMemory";
        public FileStorageReference CreateReference(Guid fileId, string extension) => new()
            { StorageProvider = StorageProvider, BucketName = "synthetic", ObjectKey = fileId.ToString("N") + extension };
        public async Task<FileStorageSaveResult> SaveAsync(FileStorageSaveRequest request, CancellationToken cancellationToken = default)
        {
            if (FailSave) throw new IOException("Synthetic storage failure");
            using var stream = new MemoryStream(); await request.Content.CopyToAsync(stream, cancellationToken);
            _content[request.Reference.ObjectKey] = stream.ToArray();
            return new() { StorageProvider = StorageProvider, BucketName = request.Reference.BucketName, ObjectKey = request.Reference.ObjectKey };
        }
        public Task<Stream> OpenReadAsync(FileResource fileResource, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(_content[fileResource.ObjectKey]));
        public Task DeleteAsync(FileResource fileResource, CancellationToken cancellationToken = default)
        { _content.Remove(fileResource.ObjectKey); return Task.CompletedTask; }
    }
}
