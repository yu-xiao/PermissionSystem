using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Authentication;

public sealed class UserAuthenticationStateTests
{
    private static readonly Guid OtherTenantId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldRejectDisabledOrDeletedUser()
    {
        await using var fixture = CreateFixture();
        var disabledUser = CreateUser(TestIds.TenantId, isEnabled: false);
        var deletedUser = CreateUser(TestIds.TenantId);
        fixture.DbContext.AddRange(
            CreateTenant(TestIds.TenantId),
            disabledUser,
            deletedUser);
        await fixture.SaveAsync();

        deletedUser.IsDeleted = true;
        await fixture.SaveAsync();

        var validator = CreateValidator(fixture.DbContext);

        Assert.Null(await validator.GetAuthenticationStateAsync(TestIds.TenantId, disabledUser.Id));
        Assert.Null(await validator.GetAuthenticationStateAsync(TestIds.TenantId, deletedUser.Id));
    }

    [Theory]
    [InlineData(TenantStatus.Initializing)]
    [InlineData(TenantStatus.Disabled)]
    [InlineData(TenantStatus.Archived)]
    [InlineData(TenantStatus.Failed)]
    public async Task GetAuthenticationStateAsync_ShouldRejectInactiveTenant(TenantStatus status)
    {
        await using var fixture = CreateFixture();
        var user = CreateUser(TestIds.TenantId);
        fixture.DbContext.AddRange(CreateTenant(TestIds.TenantId, status), user);
        await fixture.SaveAsync();

        var result = await CreateValidator(fixture.DbContext)
            .GetAuthenticationStateAsync(TestIds.TenantId, user.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnEnabledRolesAndLatestPermissions()
    {
        await using var fixture = CreateFixture();
        var user = CreateUser(TestIds.TenantId);
        var currentPermission = CreatePermission(TestIds.TenantId, "system:user:view");
        var nextPermission = CreatePermission(TestIds.TenantId, "system:user:update");
        var disabledRolePermission = CreatePermission(TestIds.TenantId, "system:user:delete");
        var currentRole = CreateRole(TestIds.TenantId, "UserAdmin", isEnabled: true);
        var disabledRole = CreateRole(TestIds.TenantId, "DisabledAdmin", isEnabled: false);
        var currentUserRole = CreateUserRole(TestIds.TenantId, user, currentRole);
        var disabledUserRole = CreateUserRole(TestIds.TenantId, user, disabledRole);
        var currentRolePermission = CreateRolePermission(TestIds.TenantId, currentRole, currentPermission);
        var disabledRelation = CreateRolePermission(TestIds.TenantId, disabledRole, disabledRolePermission);

        fixture.DbContext.AddRange(
            CreateTenant(TestIds.TenantId),
            user,
            currentPermission,
            nextPermission,
            disabledRolePermission,
            currentRole,
            disabledRole,
            currentUserRole,
            disabledUserRole,
            currentRolePermission,
            disabledRelation);
        await fixture.SaveAsync();

        var validator = CreateValidator(fixture.DbContext);
        var initialState = await validator.GetAuthenticationStateAsync(TestIds.TenantId, user.Id);

        Assert.NotNull(initialState);
        Assert.Equal(["UserAdmin"], initialState.Roles);
        Assert.Equal(["system:user:view"], initialState.PermissionCodes);

        currentRolePermission.IsDeleted = true;
        fixture.DbContext.RolePermissions.Add(
            CreateRolePermission(TestIds.TenantId, currentRole, nextPermission));
        await fixture.SaveAsync();

        var refreshedState = await validator.GetAuthenticationStateAsync(TestIds.TenantId, user.Id);

        Assert.NotNull(refreshedState);
        Assert.Equal(["UserAdmin"], refreshedState.Roles);
        Assert.Equal(["system:user:update"], refreshedState.PermissionCodes);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldRejectCrossTenantUserAndExcludeCrossTenantRelations()
    {
        await using var fixture = CreateFixture();
        var user = CreateUser(TestIds.TenantId);
        var otherTenantUser = CreateUser(OtherTenantId);
        var otherTenantRole = CreateRole(OtherTenantId, "OtherTenantAdmin", isEnabled: true);
        var otherTenantPermission = CreatePermission(OtherTenantId, "other-tenant:secret");

        fixture.DbContext.AddRange(
            CreateTenant(TestIds.TenantId),
            CreateTenant(OtherTenantId),
            user,
            otherTenantUser,
            otherTenantRole,
            otherTenantPermission,
            CreateUserRole(TestIds.TenantId, user, otherTenantRole),
            CreateRolePermission(OtherTenantId, otherTenantRole, otherTenantPermission));
        await fixture.SaveAsync();

        var validator = CreateValidator(fixture.DbContext);
        var targetState = await validator.GetAuthenticationStateAsync(TestIds.TenantId, user.Id);
        var crossTenantState = await validator.GetAuthenticationStateAsync(TestIds.TenantId, otherTenantUser.Id);

        Assert.NotNull(targetState);
        Assert.Empty(targetState.Roles);
        Assert.Empty(targetState.PermissionCodes);
        Assert.Null(crossTenantState);
    }

    private static TestFixture CreateFixture()
    {
        var tenantContext = new TenantContext();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var dbContext = new AppDbContext(options, tenantContext, new NullAuditContext());
        var systemTenantScope = new SystemTenantScope(
            tenantContext,
            NullLogger<SystemTenantScope>.Instance);
        return new TestFixture(dbContext, systemTenantScope);
    }

    private static UserCredentialValidator CreateValidator(AppDbContext dbContext)
    {
        return new UserCredentialValidator(dbContext, new PasswordHasher<User>());
    }

    private static Tenant CreateTenant(
        Guid tenantId,
        TenantStatus status = TenantStatus.Active)
    {
        return new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Code = $"tenant-{tenantId:N}",
            Name = $"Tenant {tenantId:N}",
            Status = status,
            StatusChangedAt = DateTimeOffset.UtcNow
        };
    }

    private static User CreateUser(Guid tenantId, bool isEnabled = true)
    {
        var id = Guid.NewGuid();
        return new User
        {
            Id = id,
            TenantId = tenantId,
            UserName = $"user-{id:N}",
            NormalizedUserName = $"USER-{id:N}",
            DisplayName = "Authentication state test user",
            PasswordHash = "test-password-hash",
            IsEnabled = isEnabled
        };
    }

    private static Role CreateRole(Guid tenantId, string code, bool isEnabled)
    {
        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = code,
            IsEnabled = isEnabled
        };
    }

    private static Permission CreatePermission(Guid tenantId, string code)
    {
        return new Permission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = code
        };
    }

    private static UserRole CreateUserRole(Guid tenantId, User user, Role role)
    {
        return new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            RoleId = role.Id
        };
    }

    private static RolePermission CreateRolePermission(
        Guid tenantId,
        Role role,
        Permission permission)
    {
        return new RolePermission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RoleId = role.Id,
            PermissionId = permission.Id
        };
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        public TestFixture(AppDbContext dbContext, SystemTenantScope systemTenantScope)
        {
            DbContext = dbContext;
            SystemTenantScope = systemTenantScope;
        }

        public AppDbContext DbContext { get; }

        private SystemTenantScope SystemTenantScope { get; }

        public async Task SaveAsync()
        {
            using (SystemTenantScope.Begin("AuthenticationStateTestDataSetup"))
            {
                await DbContext.SaveChangesAsync();
            }
        }

        public ValueTask DisposeAsync()
        {
            return DbContext.DisposeAsync();
        }
    }
}
