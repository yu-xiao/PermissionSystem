using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Application.Users;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;
using OpenIddict.Abstractions;

namespace PermissionSystem.UnitTests.Tenancy;

public sealed class TenantWriteConsistencyTests
{
    private static readonly Guid OtherTenantId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    [Fact]
    public void Resolver_ShouldUseCurrentTenantForNormalUser()
    {
        var currentUser = new TestCurrentUserService { TenantId = TestIds.TenantId };
        var tenantContext = CreateTenantContext(TestIds.TenantId, "Claims");
        var resolver = new TenantWriteResolver(tenantContext, currentUser);

        Assert.Equal(TestIds.TenantId, resolver.ResolveTenantId());
        Assert.Equal(TestIds.TenantId, resolver.ResolveTenantId(TestIds.TenantId));
    }

    [Fact]
    public void Resolver_ShouldRejectCrossTenantWriteForNormalUser()
    {
        var currentUser = new TestCurrentUserService { TenantId = TestIds.TenantId };
        var resolver = new TenantWriteResolver(
            CreateTenantContext(TestIds.TenantId, "Claims"),
            currentUser);

        var exception = Assert.Throws<BusinessException>(() => resolver.ResolveTenantId(OtherTenantId));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task UserCreate_ShouldRejectCrossTenantRequestBeforeAddingEntity()
    {
        var users = new InMemoryRepository<User>();
        var currentUser = new TestCurrentUserService { TenantId = TestIds.TenantId };
        var tenantContext = CreateTenantContext(TestIds.TenantId, "Claims");
        var service = new UserService(
            users,
            new InMemoryRepository<Role>(),
            new InMemoryRepository<UserRole>(),
            new InMemoryRepository<Department>(),
            new TestPasswordHashService(),
            new TestExcelService(),
            currentUser,
            new TenantWriteResolver(tenantContext, currentUser),
            new TestCacheService(),
            new TestSecurityPolicyService(),
            new TestUserSessionService(),
            new TestTokenRevocationService(),
            NullLogger<UserService>.Instance,
            new TestUnitOfWork(),
            new InMemoryAsyncQueryExecutor());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(new CreateUserRequest
        {
            TenantId = OtherTenantId,
            UserName = "cross-tenant-user",
            Password = "Password1!",
            DisplayName = "Cross tenant user"
        }));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Empty(users.Items);
    }

    [Fact]
    public void Resolver_ShouldRequireExplicitTenantForSuperAdmin()
    {
        var currentUser = new TestCurrentUserService(isSuperAdmin: true);
        var tenantContext = CreateTenantContext(TestIds.TenantId, "Default", isSuperAdmin: true);
        var resolver = new TenantWriteResolver(tenantContext, currentUser);

        var exception = Assert.Throws<BusinessException>(() => resolver.ResolveTenantId());

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public void Resolver_ShouldAcceptAndRecordExplicitRequestTenantForSuperAdmin()
    {
        var currentUser = new TestCurrentUserService(isSuperAdmin: true);
        var tenantContext = CreateTenantContext(TestIds.TenantId, "Default", isSuperAdmin: true);
        var resolver = new TenantWriteResolver(tenantContext, currentUser);

        var resolvedTenantId = resolver.ResolveTenantId(OtherTenantId);

        Assert.Equal(OtherTenantId, resolvedTenantId);
        Assert.Equal(OtherTenantId, tenantContext.TenantId);
        Assert.Equal("Request", tenantContext.Source);
        Assert.False(tenantContext.IsSystemScopeActive);
    }

    [Fact]
    public void Resolver_ShouldRejectConflictingHeaderAndRequestTenant()
    {
        var currentUser = new TestCurrentUserService(isSuperAdmin: true);
        var tenantContext = CreateTenantContext(TestIds.TenantId, "Header", isSuperAdmin: true);
        var resolver = new TenantWriteResolver(tenantContext, currentUser);

        var exception = Assert.Throws<BusinessException>(() => resolver.ResolveTenantId(OtherTenantId));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectAddedEntityFromAnotherTenant()
    {
        var tenantContext = CreateTenantContext(TestIds.TenantId, "Claims");
        await using var dbContext = CreateDbContext(tenantContext);
        dbContext.Users.Add(CreateUser(OtherTenantId));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => dbContext.SaveChangesAsync());

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectTenantIdModification()
    {
        var tenantContext = CreateTenantContext(TestIds.TenantId, "Claims");
        await using var dbContext = CreateDbContext(tenantContext);
        var user = CreateUser(TestIds.TenantId);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        user.TenantId = OtherTenantId;

        var exception = await Assert.ThrowsAsync<BusinessException>(() => dbContext.SaveChangesAsync());

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectDeletingEntityFromAnotherTenant()
    {
        var tenantContext = new TenantContext();
        await using var dbContext = CreateDbContext(tenantContext);
        var user = CreateUser(OtherTenantId);
        dbContext.Users.Add(user);
        using (CreateSystemTenantScope(tenantContext).Begin("TestDataSetup"))
        {
            await dbContext.SaveChangesAsync();
        }

        tenantContext.SetTenant(TestIds.TenantId, "Claims");
        dbContext.Users.Remove(user);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => dbContext.SaveChangesAsync());

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectWriteWithoutTenantContext()
    {
        await using var dbContext = CreateDbContext(new TenantContext());
        var user = CreateUser(OtherTenantId);
        dbContext.Users.Add(user);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => dbContext.SaveChangesAsync());

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public async Task SaveChanges_ShouldAllowWriteInsideExplicitSystemScope()
    {
        var tenantContext = new TenantContext();
        await using var dbContext = CreateDbContext(tenantContext);
        var user = CreateUser(OtherTenantId);
        dbContext.Users.Add(user);

        using (CreateSystemTenantScope(tenantContext).Begin("UnitTest"))
        {
            await dbContext.SaveChangesAsync();
        }

        Assert.Equal(OtherTenantId, user.TenantId);
        Assert.False(tenantContext.IsSystemScopeActive);
    }

    [Fact]
    public async Task Query_ShouldFailClosedWithoutTenantAndAllowExplicitSystemScope()
    {
        var tenantContext = new TenantContext();
        await using var dbContext = CreateDbContext(tenantContext);
        dbContext.Users.AddRange(CreateUser(TestIds.TenantId), CreateUser(OtherTenantId));

        using (CreateSystemTenantScope(tenantContext).Begin("TestDataSetup"))
        {
            await dbContext.SaveChangesAsync();
        }

        Assert.Empty(await dbContext.Users.ToListAsync());

        tenantContext.SetTenant(TestIds.TenantId, "Test");
        Assert.Single(await dbContext.Users.ToListAsync());

        using (CreateSystemTenantScope(tenantContext).Begin("CrossTenantRead"))
        {
            Assert.Equal(2, await dbContext.Users.CountAsync());
        }
    }

    [Fact]
    public void SystemScope_ShouldRejectHttpRequestAndRestoreNestedState()
    {
        var tenantContext = new TenantContext();
        var systemScope = CreateSystemTenantScope(tenantContext);

        using (systemScope.Begin("Outer"))
        {
            using (systemScope.Begin("Inner"))
            {
                Assert.True(tenantContext.IsSystemScopeActive);
            }

            Assert.True(tenantContext.IsSystemScopeActive);
        }

        Assert.False(tenantContext.IsSystemScopeActive);
        tenantContext.MarkAsHttpRequest();

        var exception = Assert.Throws<BusinessException>(() => systemScope.Begin("HttpAttempt"));
        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task TenantMiddleware_ShouldMarkHttpRequestWithoutOpeningSystemScopeForSuperAdmin()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(OpenIddictConstants.Claims.Role, ClaimConstants.SuperAdminRoleCode),
                new Claim(ClaimConstants.TenantId, TestIds.TenantId.ToString())
            ],
            "Test"))
        };
        var tenantContext = new TenantContext();
        var middleware = new TenantMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(
            httpContext,
            new FixedTenantResolver(new TenantResolveResult(TestIds.TenantId, "Claims")),
            tenantContext);

        Assert.True(tenantContext.IsHttpRequest);
        Assert.True(tenantContext.IsSuperAdmin);
        Assert.False(tenantContext.IsSystemScopeActive);
        Assert.Throws<BusinessException>(() =>
            CreateSystemTenantScope(tenantContext).Begin("HttpRequestAttempt"));
    }

    [Fact]
    public async Task TenantDirectory_ShouldRequireSuperAdministratorAndOnlyExposeTenantEntities()
    {
        var tenantContext = new TenantContext();
        await using var dbContext = CreateDbContext(tenantContext);
        using (CreateSystemTenantScope(tenantContext).Begin("TestDataSetup"))
        {
            dbContext.Tenants.AddRange(
                CreateTenant(TestIds.TenantId, "tenant-a"),
                CreateTenant(OtherTenantId, "tenant-b"));
            await dbContext.SaveChangesAsync();
        }

        var normalDirectory = new TenantDirectoryRepository(dbContext, new TestCurrentUserService());
        var forbidden = Assert.Throws<BusinessException>(() => normalDirectory.Query());
        Assert.Equal(ErrorCode.Forbidden, forbidden.ErrorCode);

        var superAdminDirectory = new TenantDirectoryRepository(
            dbContext,
            new TestCurrentUserService(isSuperAdmin: true));
        Assert.Equal(2, superAdminDirectory.Query().Count());
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectSuperAdminWriteWithoutExplicitTenant()
    {
        var tenantContext = CreateTenantContext(TestIds.TenantId, "Default", isSuperAdmin: true);
        await using var dbContext = CreateDbContext(tenantContext);
        dbContext.Users.Add(CreateUser(TestIds.TenantId));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => dbContext.SaveChangesAsync());

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Theory]
    [InlineData(ErrorCode.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorCode.ValidationFailed, StatusCodes.Status422UnprocessableEntity)]
    public async Task GlobalExceptionMiddleware_ShouldMapTenantWriteErrors(
        ErrorCode errorCode,
        int expectedStatusCode)
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new BusinessException(errorCode, "Tenant write failed."),
            NullLogger<GlobalExceptionMiddleware>.Instance,
            new TestHostEnvironment());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
    }

    private static AppDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options, tenantContext, new NullAuditContext());
    }

    private static SystemTenantScope CreateSystemTenantScope(TenantContext tenantContext)
    {
        return new SystemTenantScope(tenantContext, NullLogger<SystemTenantScope>.Instance);
    }

    private static TenantContext CreateTenantContext(
        Guid tenantId,
        string source,
        bool isSuperAdmin = false)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId, source);
        tenantContext.MarkAsSuperAdmin(isSuperAdmin);
        return tenantContext;
    }

    private static User CreateUser(Guid tenantId)
    {
        var id = Guid.NewGuid();
        return new User
        {
            Id = id,
            TenantId = tenantId,
            UserName = $"user-{id:N}",
            NormalizedUserName = $"USER-{id:N}",
            DisplayName = "Tenant write test",
            PasswordHash = "test-password-hash"
        };
    }

    private static Tenant CreateTenant(Guid tenantId, string code)
    {
        return new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Code = code,
            Name = code,
            Status = TenantStatus.Active,
            StatusChangedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "PermissionSystem.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FixedTenantResolver : ITenantResolver
    {
        private readonly TenantResolveResult _result;

        public FixedTenantResolver(TenantResolveResult result)
        {
            _result = result;
        }

        public TenantResolveResult Resolve(HttpContext context)
        {
            return _result;
        }
    }
}
