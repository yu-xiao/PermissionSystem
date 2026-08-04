using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Application.Users;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

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
            NullLogger<UserService>.Instance,
            new TestUnitOfWork());

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
        tenantContext.DisableTenantFilter();
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
        Assert.False(tenantContext.IsTenantFilterDisabled);
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
        await dbContext.SaveChangesAsync();

        tenantContext.SetTenant(TestIds.TenantId, "Claims");
        dbContext.Users.Remove(user);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => dbContext.SaveChangesAsync());

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task SaveChanges_ShouldAllowExplicitSystemWriteWithoutTenantContext()
    {
        await using var dbContext = CreateDbContext(new TenantContext());
        var user = CreateUser(OtherTenantId);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();

        Assert.Equal(OtherTenantId, user.TenantId);
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

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "PermissionSystem.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
