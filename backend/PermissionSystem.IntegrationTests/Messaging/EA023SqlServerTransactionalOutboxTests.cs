using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Queries;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Infrastructure.UnitOfWork;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.IntegrationTests.Messaging;

public sealed class EA023SqlServerTransactionalOutboxTests
{
    private const string ConnectionEnvName = "PERMISSION_SYSTEM_SQLSERVER_TEST_CONNECTION";

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task RolledBackBusinessTransaction_ShouldNotPersistOutboxMessage()
    {
        var tenantId = Guid.NewGuid();
        var userName = $"ea023-{Guid.NewGuid():N}";
        var messageId = Guid.NewGuid().ToString("N");

        await using (var setup = CreateContext(tenantId))
        {
            await setup.Database.MigrateAsync();
            setup.Tenants.Add(CreateTenant(tenantId));
            await setup.SaveChangesAsync();
        }

        try
        {
            await using var context = CreateContext(tenantId);
            var unitOfWork = new UnitOfWork(context);
            var userRepository = new Repository<User>(context);
            var outbox = CreateOutboxService(context, tenantId);

            await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await userRepository.AddAsync(CreateUser(tenantId, userName), token);
                await EnqueueAsync(outbox, messageId, tenantId, token);
                await unitOfWork.SaveChangesAsync(token);
                throw new InvalidOperationException("Simulate business transaction rollback.");
            }));

            await using var verification = CreateContext(tenantId);
            Assert.False(await verification.Users.IgnoreQueryFilters()
                .AnyAsync(entity => entity.TenantId == tenantId && entity.UserName == userName));
            Assert.False(await verification.OutboxMessages.IgnoreQueryFilters()
                .AnyAsync(entity => entity.TenantId == tenantId && entity.MessageId == messageId));
        }
        finally
        {
            await CleanupAsync(tenantId);
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task OutboxWriteFailure_ShouldRollBackBusinessWrite()
    {
        var tenantId = Guid.NewGuid();
        var userName = $"ea023-{Guid.NewGuid():N}";
        var messageId = Guid.NewGuid().ToString("N");

        await using (var setup = CreateContext(tenantId))
        {
            await setup.Database.MigrateAsync();
            setup.Tenants.Add(CreateTenant(tenantId));
            setup.OutboxMessages.Add(new OutboxMessage
            {
                TenantId = tenantId,
                MessageId = messageId,
                Exchange = "notifications",
                RoutingKey = "notification.created",
                MessageType = "NotificationCreated",
                Payload = "{}",
                Status = "Pending"
            });
            await setup.SaveChangesAsync();
        }

        try
        {
            await using var context = CreateContext(tenantId);
            var unitOfWork = new UnitOfWork(context);
            var userRepository = new Repository<User>(context);
            var outbox = CreateOutboxService(context, tenantId);

            var exception = await Assert.ThrowsAsync<BusinessException>(() => unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await userRepository.AddAsync(CreateUser(tenantId, userName), token);
                await EnqueueAsync(outbox, messageId, tenantId, token);
                await unitOfWork.SaveChangesAsync(token);
            }));

            Assert.Equal(ErrorCode.Conflict, exception.ErrorCode);

            await using var verification = CreateContext(tenantId);
            Assert.False(await verification.Users.IgnoreQueryFilters()
                .AnyAsync(entity => entity.TenantId == tenantId && entity.UserName == userName));
            Assert.Equal(1, await verification.OutboxMessages.IgnoreQueryFilters()
                .CountAsync(entity => entity.TenantId == tenantId && entity.MessageId == messageId));
        }
        finally
        {
            await CleanupAsync(tenantId);
        }
    }

    private static OutboxService CreateOutboxService(AppDbContext context, Guid tenantId)
    {
        return new OutboxService(
            new Repository<OutboxMessage>(context),
            new TestCurrentUserService(tenantId),
            new TraceContextAccessor { TraceId = "ea-023" },
            new EfCoreAsyncQueryExecutor());
    }

    private static Task<string> EnqueueAsync(
        IOutboxService outbox,
        string messageId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return outbox.EnqueueAsync(new CreateOutboxMessageRequest
        {
            TenantId = tenantId,
            MessageId = messageId,
            Exchange = "notifications",
            RoutingKey = "notification.created",
            MessageType = "NotificationCreated",
            Payload = "{}"
        }, cancellationToken);
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable(ConnectionEnvName)!)
            .Options;
        return new AppDbContext(options, new TestTenantContext(tenantId), new NullAuditContext());
    }

    private static User CreateUser(Guid tenantId, string userName)
    {
        return new User
        {
            TenantId = tenantId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = "EA-023 test user",
            PasswordHash = "test"
        };
    }

    private static async Task CleanupAsync(Guid tenantId)
    {
        await using var context = CreateContext(tenantId);
        await context.OutboxMessages.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == tenantId)
            .ExecuteDeleteAsync();
        await context.Users.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == tenantId)
            .ExecuteDeleteAsync();
        await context.Tenants.IgnoreQueryFilters()
            .Where(entity => entity.Id == tenantId)
            .ExecuteDeleteAsync();
    }

    private static Tenant CreateTenant(Guid tenantId) => new()
    {
        Id = tenantId,
        TenantId = tenantId,
        Code = $"ea023-{tenantId:N}",
        Name = "EA-023 test tenant",
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

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(Guid tenantId)
        {
            TenantId = tenantId;
        }

        public bool IsAuthenticated => true;
        public Guid? UserId => null;
        public Guid? TenantId { get; }
        public Guid? DepartmentId => null;
        public string? SessionId => null;
        public string? Username => null;
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> PermissionCodes => [];
        public bool IsSuperAdmin => false;
        public bool IsCurrentUserSuperAdmin() => false;
        public bool IsCurrentUserAdmin() => false;
        public bool CanManageBuiltinResources() => false;
        public bool HasPermission(string permissionCode) => false;
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
        public void SetTenant(Guid tenantId, string source) { }
        public void MarkAsSuperAdmin(bool isSuperAdmin) { }
        public void MarkAsHttpRequest() { }
    }
}
