using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Application;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.OperationLogs;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Auditing;

public sealed class AuditPersistenceTests
{
    [Fact]
    public void AddApplication_ShouldRegisterNullAuditContextByDefault()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<NullAuditContext>(scope.ServiceProvider.GetRequiredService<IAuditContext>());
    }

    [Fact]
    public void AddApplication_ShouldPreserveHostAuditContextRegistration()
    {
        var expected = new MutableAuditContext(TestIds.AdminUserId);
        var services = new ServiceCollection();
        services.AddScoped<IAuditContext>(_ => expected);
        services.AddApplication();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Same(expected, scope.ServiceProvider.GetRequiredService<IAuditContext>());
    }

    [Fact]
    public async Task SaveChanges_ShouldPopulateCreationAndModificationActors()
    {
        var auditContext = new MutableAuditContext(TestIds.AdminUserId);
        await using var dbContext = CreateDbContext(auditContext);
        var user = CreateUser();

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        Assert.Equal(TestIds.AdminUserId, user.CreatedBy);
        Assert.Null(user.UpdatedBy);

        auditContext.UserId = TestIds.NormalUserId;
        user.DisplayName = "Updated user";
        await dbContext.SaveChangesAsync();

        Assert.Equal(TestIds.AdminUserId, user.CreatedBy);
        Assert.Equal(TestIds.NormalUserId, user.UpdatedBy);
        Assert.NotNull(user.UpdatedAt);

        auditContext.UserId = TestIds.ApproverUserId;
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync();

        Assert.True(user.IsDeleted);
        Assert.Equal(TestIds.AdminUserId, user.CreatedBy);
        Assert.Equal(TestIds.ApproverUserId, user.UpdatedBy);
    }

    [Fact]
    public async Task SaveChanges_ShouldPreserveExplicitCreatedByAndAllowSystemActor()
    {
        var explicitCreator = Guid.NewGuid();
        await using var userContext = CreateDbContext(new MutableAuditContext(TestIds.AdminUserId));
        var explicitUser = CreateUser();
        explicitUser.CreatedBy = explicitCreator;

        userContext.Users.Add(explicitUser);
        await userContext.SaveChangesAsync();

        Assert.Equal(explicitCreator, explicitUser.CreatedBy);

        await using var systemContext = CreateDbContext(new NullAuditContext());
        var systemUser = CreateUser();
        systemContext.Users.Add(systemUser);
        await systemContext.SaveChangesAsync();

        Assert.Null(systemUser.CreatedBy);

        var explicitUpdater = Guid.NewGuid();
        systemUser.DisplayName = "System updated user";
        systemUser.UpdatedBy = explicitUpdater;
        await systemContext.SaveChangesAsync();

        Assert.Equal(explicitUpdater, systemUser.UpdatedBy);
    }

    [Fact]
    public async Task OperationLogMiddleware_ShouldNotCommitTrackedBusinessChangesWhenRequestFails()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var databaseRoot = new InMemoryDatabaseRoot();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext>(_ => CreateTenantContext());
        services.AddScoped<IAuditContext>(_ => new MutableAuditContext(TestIds.AdminUserId));
        services.AddScoped<ICurrentUserService>(_ => new TestCurrentUserService(TestIds.AdminUserId));
        services.AddScoped<ITraceContextAccessor, TraceContextAccessor>();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, PermissionSystem.Infrastructure.UnitOfWork.UnitOfWork>();
        services.AddScoped<IOperationLogService, OperationLogService>();

        await using var provider = services.BuildServiceProvider();
        await using var requestScope = provider.CreateAsyncScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestScope.ServiceProvider
        };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/test";
        httpContext.Response.Body = new MemoryStream();
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var middleware = new OperationLogMiddleware(
            async context =>
            {
                var requestDbContext = context.RequestServices.GetRequiredService<AppDbContext>();
                await requestDbContext.Users.AddAsync(CreateUser());
                throw new InvalidOperationException("Simulated request failure.");
            },
            requestScope.ServiceProvider.GetRequiredService<ILogger<OperationLogMiddleware>>(),
            provider.GetRequiredService<IServiceScopeFactory>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(
            httpContext,
            requestScope.ServiceProvider.GetRequiredService<ICurrentUserService>(),
            requestScope.ServiceProvider.GetRequiredService<ITraceContextAccessor>()));

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Empty(await verificationDbContext.Users.ToListAsync());
        var operationLog = Assert.Single(await verificationDbContext.OperationLogs.ToListAsync());
        Assert.Equal(TestIds.AdminUserId, operationLog.UserId);
        Assert.Equal(TestIds.AdminUserId, operationLog.CreatedBy);
        Assert.Equal(StatusCodes.Status500InternalServerError, operationLog.StatusCode);
    }

    private static AppDbContext CreateDbContext(IAuditContext auditContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options, CreateTenantContext(), auditContext);
    }

    private static TenantContext CreateTenantContext()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Test");
        return tenantContext;
    }

    private static User CreateUser()
    {
        var id = Guid.NewGuid();
        return new User
        {
            Id = id,
            TenantId = TestIds.TenantId,
            UserName = $"user-{id:N}",
            NormalizedUserName = $"USER-{id:N}",
            DisplayName = "Test user",
            PasswordHash = "test-password-hash"
        };
    }

    private sealed class MutableAuditContext : IAuditContext
    {
        public MutableAuditContext(Guid? userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; set; }
    }
}
