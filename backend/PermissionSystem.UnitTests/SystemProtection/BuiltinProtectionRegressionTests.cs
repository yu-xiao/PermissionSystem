using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Roles;
using PermissionSystem.Application.Users;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.SystemProtection;

public sealed class BuiltinProtectionRegressionTests
{
    [Fact]
    public async Task AdminUser_CannotBeDeleted()
    {
        var admin = CreateAdmin();
        var service = CreateUserService(new InMemoryRepository<User>(admin));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(admin.Id));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task AdminUser_CannotBeDisabled()
    {
        var admin = CreateAdmin();
        var service = CreateUserService(new InMemoryRepository<User>(admin));

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SetEnabledAsync(admin.Id, new SetUserEnabledRequest { IsEnabled = false }));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task SuperAdminRole_CannotBeDeleted()
    {
        var role = CreateSuperAdminRole(isBuiltin: true);
        var service = CreateRoleService(new InMemoryRepository<Role>(role));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(role.Id));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task NormalAdmin_CannotAssignSuperAdminRole()
    {
        var user = CreateNormalUser();
        var superAdminRole = CreateSuperAdminRole();
        var service = CreateUserService(
            new InMemoryRepository<User>(user),
            new InMemoryRepository<Role>(superAdminRole),
            currentUser: new TestCurrentUserService(
                TestIds.AdminUserId,
                isSuperAdmin: false,
                permissions: ["system:user:update"]));

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AssignRolesAsync(user.Id, new AssignUserRolesRequest { RoleIds = [superAdminRole.Id] }));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task CannotRemoveLastSuperAdminUser()
    {
        var protectedUser = CreateNormalUser(TestIds.AdminUserId);
        var superAdminRole = CreateSuperAdminRole();
        var relation = new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            UserId = protectedUser.Id,
            RoleId = superAdminRole.Id
        };
        var service = CreateUserService(
            new InMemoryRepository<User>(protectedUser),
            new InMemoryRepository<Role>(superAdminRole),
            new InMemoryRepository<UserRole>(relation),
            new TestCurrentUserService(Guid.NewGuid(), isSuperAdmin: true));

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AssignRolesAsync(protectedUser.Id, new AssignUserRolesRequest { RoleIds = [] }));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    private static UserService CreateUserService(
        InMemoryRepository<User> users,
        InMemoryRepository<Role>? roles = null,
        InMemoryRepository<UserRole>? userRoles = null,
        TestCurrentUserService? currentUser = null)
    {
        return new UserService(
            users,
            roles ?? new InMemoryRepository<Role>(),
            userRoles ?? new InMemoryRepository<UserRole>(),
            new InMemoryRepository<Department>(),
            new TestPasswordHashService(),
            new TestExcelService(),
            currentUser ?? new TestCurrentUserService(TestIds.NormalUserId),
            new TestTenantWriteResolver(),
            new TestCacheService(),
            new TestSecurityPolicyService(),
            new TestUserSessionService(),
            new TestTokenRevocationService(),
            NullLogger<UserService>.Instance,
            new TestUnitOfWork());
    }

    private static RoleService CreateRoleService(InMemoryRepository<Role> roles)
    {
        return new RoleService(
            roles,
            new InMemoryRepository<RoleMenu>(),
            new InMemoryRepository<RolePermission>(),
            new InMemoryRepository<Menu>(),
            new InMemoryRepository<Permission>(),
            new InMemoryRepository<User>(),
            new InMemoryRepository<UserRole>(),
            new InMemoryRepository<RoleDataScope>(),
            new InMemoryRepository<Department>(),
            new TestCurrentUserService(TestIds.NormalUserId),
            new TestTenantWriteResolver(),
            new TestCacheService(),
            new TestSecurityPolicyService(),
            NullLogger<RoleService>.Instance,
            new TestUnitOfWork());
    }

    private static User CreateAdmin()
    {
        return new User
        {
            Id = TestIds.AdminUserId,
            TenantId = TestIds.TenantId,
            UserName = SystemBuiltinConstants.AdminUserName,
            NormalizedUserName = SystemBuiltinConstants.AdminNormalizedUserName,
            DisplayName = "System Administrator",
            PasswordHash = "hashed",
            IsEnabled = true,
            IsBuiltin = true
        };
    }

    private static User CreateNormalUser(Guid? userId = null)
    {
        return new User
        {
            Id = userId ?? TestIds.NormalUserId,
            TenantId = TestIds.TenantId,
            UserName = "normal-admin",
            NormalizedUserName = "NORMAL-ADMIN",
            DisplayName = "Normal Admin",
            PasswordHash = "hashed",
            IsEnabled = true
        };
    }

    private static Role CreateSuperAdminRole(bool isBuiltin = false)
    {
        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = SystemBuiltinConstants.SuperAdminRoleCode,
            Name = SystemBuiltinConstants.SuperAdminRoleName,
            IsEnabled = true,
            IsBuiltin = isBuiltin
        };
    }

    private sealed class TestPasswordHashService : PermissionSystem.Application.Abstractions.IPasswordHashService
    {
        public string HashPassword(string password) => "hashed:" + password;
        public bool VerifyPassword(string passwordHash, string password) => passwordHash == HashPassword(password);
    }
}
