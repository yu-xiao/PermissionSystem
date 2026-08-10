using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Roles;
using PermissionSystem.Application.Users;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Authorization;

public sealed class AuthorizationInvalidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UserStatusChange_ShouldRotateStampAndRevokeAllAuthentication(bool enableUser)
    {
        var user = CreateUser(isEnabled: !enableUser);
        var originalStamp = user.SecurityStamp;
        var sessions = new TestUserSessionService();
        var tokens = new TestTokenRevocationService();
        var service = CreateUserService(user, sessions, tokens);

        await service.SetEnabledAsync(user.Id, new SetUserEnabledRequest { IsEnabled = enableUser });

        Assert.Equal(enableUser, user.IsEnabled);
        Assert.NotEqual(originalStamp, user.SecurityStamp);
        Assert.Contains(sessions.StagedRevocations, item => item.UserId == user.Id);
        Assert.Single(sessions.PublishedRevocations);
        Assert.Equal([user.Id], tokens.RevokedUserIds);
    }

    [Fact]
    public async Task ResetPassword_ShouldRotateStampAndRevokeAllAuthentication()
    {
        var user = CreateUser(isEnabled: true);
        var originalStamp = user.SecurityStamp;
        var sessions = new TestUserSessionService();
        var tokens = new TestTokenRevocationService();
        var service = CreateUserService(user, sessions, tokens);

        await service.ResetPasswordAsync(user.Id, new ResetUserPasswordRequest
        {
            NewPassword = "NewPassword1!"
        });

        Assert.NotEqual(originalStamp, user.SecurityStamp);
        Assert.Equal("hashed:NewPassword1!", user.PasswordHash);
        Assert.Contains(sessions.StagedRevocations, item => item.UserId == user.Id);
        Assert.Single(sessions.PublishedRevocations);
        Assert.Equal([user.Id], tokens.RevokedUserIds);
    }

    [Fact]
    public async Task AssignRoles_ShouldRotateStampWithoutRevokingSession()
    {
        var user = CreateUser(isEnabled: true);
        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "operator",
            Name = "Operator",
            IsEnabled = true
        };
        var originalStamp = user.SecurityStamp;
        var sessions = new TestUserSessionService();
        var tokens = new TestTokenRevocationService();
        var userRoles = new InMemoryRepository<UserRole>();
        var service = CreateUserService(user, sessions, tokens, role, userRoles);

        await service.AssignRolesAsync(user.Id, new AssignUserRolesRequest { RoleIds = [role.Id] });

        Assert.NotEqual(originalStamp, user.SecurityStamp);
        Assert.Contains(userRoles.Items, item => item.UserId == user.Id && item.RoleId == role.Id);
        Assert.Empty(sessions.StagedRevocations);
        Assert.Empty(tokens.RevokedUserIds);
    }

    [Fact]
    public async Task AssignRolePermissions_ShouldRotateAffectedUserStamp()
    {
        var user = CreateUser(isEnabled: true);
        var role = CreateRole(isEnabled: true);
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "orders:view",
            Name = "View orders",
            Group = "Orders"
        };
        var originalStamp = user.SecurityStamp;
        var service = CreateRoleService(
            role,
            new InMemoryRepository<User>(user),
            new InMemoryRepository<UserRole>(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                UserId = user.Id,
                RoleId = role.Id
            }),
            new InMemoryRepository<Permission>(permission));

        await service.AssignPermissionsAsync(role.Id, new AssignRolePermissionsRequest
        {
            PermissionIds = [permission.Id]
        });

        Assert.NotEqual(originalStamp, user.SecurityStamp);
    }

    [Fact]
    public async Task DisabledRole_ShouldNotGrantMenusOrDataScope()
    {
        var user = CreateUser(isEnabled: true);
        var role = CreateRole(isEnabled: false);
        var menu = new Menu
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Name = "Orders",
            Path = "/orders",
            Visible = true
        };
        var userRoles = new InMemoryRepository<UserRole>(new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            UserId = user.Id,
            RoleId = role.Id
        });
        var roleMenus = new InMemoryRepository<RoleMenu>(new RoleMenu
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            RoleId = role.Id,
            MenuId = menu.Id
        });
        var currentUser = new TestCurrentUserService(user.Id) { TenantId = TestIds.TenantId };
        var currentUserService = new CurrentUserAppService(
            currentUser,
            new InMemoryRepository<Menu>(menu),
            new InMemoryRepository<Role>(role),
            roleMenus,
            userRoles,
            new InMemoryRepository<RolePermission>(),
            new InMemoryRepository<Permission>());
        var dataScopeService = new DataScopeService(
            new InMemoryRepository<Role>(role),
            new InMemoryRepository<User>(user),
            userRoles,
            new InMemoryRepository<RoleDataScope>(new RoleDataScope
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                RoleId = role.Id,
                ScopeType = DataScopeType.All
            }),
            new InMemoryRepository<Department>(),
            currentUser,
            NullLogger<DataScopeService>.Instance,
            new TestUnitOfWork());

        var menus = await currentUserService.GetCurrentUserMenusAsync();
        var scope = await dataScopeService.GetCurrentUserDataScopeAsync();

        Assert.Empty(menus);
        Assert.Equal(DataScopeType.CurrentUser, scope.ScopeType);
    }

    [Fact]
    public async Task DeletePermission_ShouldRotateUsersAssignedThroughAffectedRoles()
    {
        var user = CreateUser(isEnabled: true);
        var role = CreateRole(isEnabled: true);
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "orders:delete",
            Name = "Delete orders",
            Group = "Orders"
        };
        var originalStamp = user.SecurityStamp;
        var service = new PermissionService(
            new InMemoryRepository<Permission>(permission),
            new InMemoryRepository<RolePermission>(new RolePermission
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                RoleId = role.Id,
                PermissionId = permission.Id
            }),
            new InMemoryRepository<UserRole>(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = TestIds.TenantId,
                UserId = user.Id,
                RoleId = role.Id
            }),
            new InMemoryRepository<User>(user),
            new TestTenantWriteResolver(),
            new TestUnitOfWork());

        await service.DeleteAsync(permission.Id);

        Assert.NotEqual(originalStamp, user.SecurityStamp);
    }

    private static UserService CreateUserService(
        User user,
        TestUserSessionService sessions,
        TestTokenRevocationService tokens,
        Role? role = null,
        InMemoryRepository<UserRole>? userRoles = null)
    {
        return new UserService(
            new InMemoryRepository<User>(user),
            role is null ? new InMemoryRepository<Role>() : new InMemoryRepository<Role>(role),
            userRoles ?? new InMemoryRepository<UserRole>(),
            new InMemoryRepository<Department>(),
            new TestPasswordHashService(),
            new TestExcelService(),
            new TestCurrentUserService(TestIds.AdminUserId, isSuperAdmin: true),
            new TestTenantWriteResolver(),
            new TestCacheService(),
            new TestSecurityPolicyService(),
            sessions,
            tokens,
            NullLogger<UserService>.Instance,
            new TestUnitOfWork(),
            new InMemoryAsyncQueryExecutor());
    }

    private static User CreateUser(bool isEnabled)
    {
        return new User
        {
            Id = TestIds.NormalUserId,
            TenantId = TestIds.TenantId,
            UserName = "normal-user",
            NormalizedUserName = "NORMAL-USER",
            DisplayName = "Normal User",
            PasswordHash = "hashed:OldPassword1!",
            IsEnabled = isEnabled
        };
    }

    private static Role CreateRole(bool isEnabled)
    {
        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "operator",
            Name = "Operator",
            IsEnabled = isEnabled
        };
    }

    private static RoleService CreateRoleService(
        Role role,
        InMemoryRepository<User> users,
        InMemoryRepository<UserRole> userRoles,
        InMemoryRepository<Permission> permissions)
    {
        return new RoleService(
            new InMemoryRepository<Role>(role),
            new InMemoryRepository<RoleMenu>(),
            new InMemoryRepository<RolePermission>(),
            new InMemoryRepository<Menu>(),
            permissions,
            users,
            userRoles,
            new InMemoryRepository<RoleDataScope>(),
            new InMemoryRepository<Department>(),
            new TestCurrentUserService(TestIds.AdminUserId, isSuperAdmin: true),
            new TestTenantWriteResolver(),
            new TestCacheService(),
            new TestSecurityPolicyService(),
            NullLogger<RoleService>.Instance,
            new TestUnitOfWork(),
            new InMemoryAsyncQueryExecutor());
    }
}
