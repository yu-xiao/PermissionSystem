using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Tenancy;

public sealed class TenantLifecycleTests
{
    private static readonly Guid TenantId = Guid.Parse("90000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Initialization_ShouldBeIdempotentAndActivateTenant()
    {
        var tenant = new Tenant
        {
            Id = TenantId,
            TenantId = TenantId,
            Code = "tenant-a",
            Name = "Tenant A",
            Status = TenantStatus.Initializing,
            StatusChangedAt = DateTimeOffset.UtcNow
        };
        var administrator = new User
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            DisplayName = "Tenant administrator",
            PasswordHash = "hashed",
            IsEnabled = true,
            IsBuiltin = true
        };
        var fixture = CreateInitializationFixture(tenant, administrator);

        await fixture.Job.ExecuteAsync(TenantId);
        await fixture.Job.ExecuteAsync(TenantId);

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(100, tenant.InitializationProgress);
        Assert.Equal("Completed", tenant.InitializationStep);
        Assert.Equal(1, tenant.InitializationAttempts);
        Assert.Single(fixture.Departments.Items);
        Assert.Single(fixture.Roles.Items);
        Assert.Equal(SystemBuiltinConstants.TenantAdminRoleCode, fixture.Roles.Items.Single().Code);
        Assert.Single(fixture.SecurityPolicies.Items);
        Assert.Equal(34, fixture.Permissions.Items.Count);
        Assert.Equal(10, fixture.Menus.Items.Count);
        Assert.Equal(34, fixture.RolePermissions.Items.Count);
        Assert.Equal(10, fixture.RoleMenus.Items.Count);
        Assert.Equal(fixture.Departments.Items.Single().Id, administrator.DepartmentId);
    }

    [Fact]
    public async Task TenantStatusMiddleware_ShouldRejectInactiveTenant()
    {
        var nextCalled = false;
        var middleware = new TenantStatusMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId, "Claims");

        await middleware.InvokeAsync(context, tenantContext, new FixedTenantStatusChecker(false));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task RevokeTenantSessions_ShouldOnlyRevokeTargetTenantAndRemainRevoked()
    {
        var target = CreateSession(TenantId, "target");
        var other = CreateSession(Guid.NewGuid(), "other");
        var repository = new InMemoryRepository<UserSession>(target, other);
        var service = new UserSessionService(
            repository,
            new TestCurrentUserService(isSuperAdmin: true),
            new TestCacheService(),
            new TestUnitOfWork());

        await service.RevokeTenantSessionsAsync(TenantId, "Tenant disabled.");

        Assert.True(target.IsRevoked);
        Assert.Equal("Tenant disabled.", target.RevokedReason);
        Assert.False(other.IsRevoked);
        Assert.True(await service.IsRevokedAsync(target.SessionId));
    }

    [Fact]
    public async Task DisableAndRestore_ShouldStopTasksAndNotRestoreRevokedSessions()
    {
        var tenant = new Tenant
        {
            Id = TenantId,
            TenantId = TenantId,
            Code = "tenant-a",
            Name = "Tenant A",
            Status = TenantStatus.Active,
            StatusChangedAt = DateTimeOffset.UtcNow
        };
        var user = new User { Id = Guid.NewGuid(), TenantId = TenantId, UserName = "admin", NormalizedUserName = "ADMIN", DisplayName = "Admin", PasswordHash = "hashed" };
        var session = CreateSession(TenantId, "active-session");
        session.UserId = user.Id;
        var sessions = new UserSessionService(
            new InMemoryRepository<UserSession>(session),
            new TestCurrentUserService(isSuperAdmin: true),
            new TestCacheService(),
            new TestUnitOfWork());
        var tokenRevocation = new RecordingTokenRevocationService();
        var scheduledTasks = new RecordingScheduledTaskService();
        var tenantContext = new TenantContext();
        tenantContext.MarkAsHttpRequest();
        var tenants = new InMemoryRepository<Tenant>(tenant);
        var service = new TenantService(
            tenants,
            new InMemoryRepository<User>(user),
            new TestTenantDirectoryRepository(tenant),
            new TestCurrentUserService(isSuperAdmin: true),
            tenantContext,
            new TestPasswordHashService(),
            sessions,
            tokenRevocation,
            scheduledTasks,
            new RecordingBackgroundJobService(),
            new TestUnitOfWork());

        await service.DisableAsync(TenantId);
        await service.DisableAsync(TenantId);
        await service.RestoreAsync(TenantId);
        await service.RestoreAsync(TenantId);

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.True(session.IsRevoked);
        Assert.Equal(2, tokenRevocation.RevokedUserIds.Count(id => id == user.Id));
        Assert.Equal([TenantId, TenantId], scheduledTasks.SuspendedTenantIds);
        Assert.Equal([TenantId, TenantId], scheduledTasks.ResumedTenantIds);
    }

    private static InitializationFixture CreateInitializationFixture(Tenant tenant, User administrator)
    {
        var tenants = new InMemoryRepository<Tenant>(tenant);
        var departments = new InMemoryRepository<Department>();
        var users = new InMemoryRepository<User>(administrator);
        var roles = new InMemoryRepository<Role>();
        var permissions = new InMemoryRepository<Permission>();
        var menus = new InMemoryRepository<Menu>();
        var userRoles = new InMemoryRepository<UserRole>();
        var rolePermissions = new InMemoryRepository<RolePermission>();
        var roleMenus = new InMemoryRepository<RoleMenu>();
        var roleDataScopes = new InMemoryRepository<RoleDataScope>();
        var securityPolicies = new InMemoryRepository<SecurityPolicy>();
        var datasets = new InMemoryRepository<McpDatasetDefinition>();
        var datasetFields = new InMemoryRepository<McpDatasetField>();
        var unitOfWork = new TestUnitOfWork();
        var tenantContext = new TenantContext();
        var job = new TenantInitializationJob(
            tenants,
            departments,
            users,
            roles,
            permissions,
            menus,
            userRoles,
            rolePermissions,
            roleMenus,
            roleDataScopes,
            securityPolicies,
            unitOfWork,
            new ImmediateDistributedLock(),
            new SystemTenantScope(tenantContext, NullLogger<SystemTenantScope>.Instance),
            new McpDatasetProvisioner(datasets, datasetFields, new InMemoryAsyncQueryExecutor(), unitOfWork),
            NullLogger<TenantInitializationJob>.Instance);

        return new InitializationFixture(job, departments, roles, permissions, menus, rolePermissions, roleMenus, securityPolicies);
    }

    private static UserSession CreateSession(Guid tenantId, string sessionId)
    {
        return new UserSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            UserName = sessionId,
            SessionId = sessionId,
            LoginAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
    }

    private sealed record InitializationFixture(
        TenantInitializationJob Job,
        InMemoryRepository<Department> Departments,
        InMemoryRepository<Role> Roles,
        InMemoryRepository<Permission> Permissions,
        InMemoryRepository<Menu> Menus,
        InMemoryRepository<RolePermission> RolePermissions,
        InMemoryRepository<RoleMenu> RoleMenus,
        InMemoryRepository<SecurityPolicy> SecurityPolicies);

    private sealed class FixedTenantStatusChecker : ITenantStatusChecker
    {
        private readonly bool _isActive;

        public FixedTenantStatusChecker(bool isActive)
        {
            _isActive = isActive;
        }

        public Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_isActive);
        }
    }

    private sealed class ImmediateDistributedLock : IDistributedLock
    {
        public Task<DistributedLockHandle?> TryAcquireAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            => Task.FromResult<DistributedLockHandle?>(new DistributedLockHandle(key, "test", expiry ?? TimeSpan.FromMinutes(1)));

        public Task<DistributedLockHandle> AcquireAsync(string key, TimeSpan? expiry = null, TimeSpan? waitTime = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new DistributedLockHandle(key, "test", expiry ?? TimeSpan.FromMinutes(1)));

        public Task<bool> ReleaseAsync(DistributedLockHandle handle, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task ExecuteWithLockAsync(string key, Func<CancellationToken, Task> action, TimeSpan? expiry = null, TimeSpan? waitTime = null, CancellationToken cancellationToken = default)
            => action(cancellationToken);

        public Task<TResult> ExecuteWithLockAsync<TResult>(string key, Func<CancellationToken, Task<TResult>> action, TimeSpan? expiry = null, TimeSpan? waitTime = null, CancellationToken cancellationToken = default)
            => action(cancellationToken);
    }

    private sealed class TestTenantDirectoryRepository : ITenantDirectoryRepository
    {
        private readonly Tenant _tenant;

        public TestTenantDirectoryRepository(Tenant tenant)
        {
            _tenant = tenant;
        }

        public IQueryable<Tenant> Query() => new[] { _tenant }.AsQueryable();

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Tenant?>(_tenant.Id == id ? _tenant : null);
    }

    private sealed class RecordingTokenRevocationService : ITokenRevocationService
    {
        public List<Guid> RevokedUserIds { get; } = [];

        public Task RevokeRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RevokeUserRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            RevokedUserIds.Add(userId);
            return Task.CompletedTask;
        }

        public async Task RevokeUsersRefreshTokensAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
        {
            foreach (var userId in userIds)
            {
                await RevokeUserRefreshTokensAsync(userId, cancellationToken);
            }
        }
    }

    private sealed class RecordingScheduledTaskService : IScheduledTaskService
    {
        public List<Guid> SuspendedTenantIds { get; } = [];
        public List<Guid> ResumedTenantIds { get; } = [];

        public Task SuspendTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            SuspendedTenantIds.Add(tenantId);
            return Task.CompletedTask;
        }

        public Task ResumeTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            ResumedTenantIds.Add(tenantId);
            return Task.CompletedTask;
        }

        public Task<PagedResult<ScheduledTaskResponse>> GetPagedAsync(ScheduledTaskQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<ScheduledTaskExecutionLogResponse>> GetLogsAsync(Guid taskId, ScheduledTaskLogQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduledTaskResponse> CreateAsync(CreateScheduledTaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduledTaskResponse> UpdateAsync(Guid id, UpdateScheduledTaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnableAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DisableAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task TriggerAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SyncEnabledTasksAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingBackgroundJobService : IBackgroundJobService
    {
        public string Enqueue<TJob>(System.Linq.Expressions.Expression<Func<TJob, Task>> methodCall) => "test-job";
        public string Schedule<TJob>(System.Linq.Expressions.Expression<Func<TJob, Task>> methodCall, TimeSpan delay) => "test-job";
        public void AddOrUpdateRecurring<TJob>(string recurringJobId, System.Linq.Expressions.Expression<Func<TJob, Task>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null, string queue = "default") { }
        public void RemoveRecurring(string recurringJobId) { }
        public void TriggerRecurring(string recurringJobId) { }
        public bool Delete(string jobId) => true;
    }
}
