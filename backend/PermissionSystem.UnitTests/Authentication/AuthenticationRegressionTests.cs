using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Authentication;

public sealed class AuthenticationRegressionTests
{
    [Fact]
    public async Task Admin_CanLogin_WithCorrectPassword()
    {
        await using var dbContext = CreateDbContext();
        var passwordHasher = new PasswordHasher<User>();
        var admin = CreateAdmin(passwordHasher, "Admin_12345");
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "system:user:view",
            Name = "View users"
        };
        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "SuperAdmin",
            Name = "SuperAdmin",
            IsEnabled = true,
            RolePermissions =
            [
                new RolePermission
                {
                    Id = Guid.NewGuid(),
                    TenantId = TestIds.TenantId,
                    Permission = permission,
                    PermissionId = permission.Id
                }
            ]
        };
        admin.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            User = admin,
            UserId = admin.Id,
            Role = role,
            RoleId = role.Id
        });

        dbContext.Users.Add(admin);
        dbContext.Roles.Add(role);
        dbContext.Permissions.Add(permission);
        await dbContext.SaveChangesAsync();

        var validator = new UserCredentialValidator(dbContext, passwordHasher);

        var result = await validator.ValidateAsync("admin", "Admin_12345");

        Assert.NotNull(result);
        Assert.Equal(TestIds.AdminUserId, result.UserId);
        Assert.Contains("SuperAdmin", result.Roles);
        Assert.Contains("system:user:view", result.PermissionCodes);
    }

    [Fact]
    public async Task Login_ShouldFail_WithWrongPassword()
    {
        await using var dbContext = CreateDbContext();
        var passwordHasher = new PasswordHasher<User>();
        dbContext.Users.Add(CreateAdmin(passwordHasher, "Admin_12345"));
        await dbContext.SaveChangesAsync();
        var validator = new UserCredentialValidator(dbContext, passwordHasher);

        var result = await validator.ValidateAsync("admin", "wrong-password");

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokenSession_IsUsableUntilRevoked()
    {
        var repository = new InMemoryRepository<UserSession>();
        var cache = new TestCacheService();
        var service = new UserSessionService(
            repository,
            new TestCurrentUserService(TestIds.AdminUserId, isSuperAdmin: true),
            cache,
            new TestUnitOfWork());

        var session = await service.CreateAsync(new CreateUserSessionRequest
        {
            TenantId = TestIds.TenantId,
            UserId = TestIds.AdminUserId,
            UserName = "admin",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });

        Assert.False(await service.IsRevokedAsync(session.SessionId));
    }

    [Fact]
    public async Task Revoke_ShouldMakeRefreshTokenSessionInvalid()
    {
        var repository = new InMemoryRepository<UserSession>();
        var cache = new TestCacheService();
        var service = new UserSessionService(
            repository,
            new TestCurrentUserService(TestIds.AdminUserId, isSuperAdmin: true),
            cache,
            new TestUnitOfWork());
        var session = await service.CreateAsync(new CreateUserSessionRequest
        {
            TenantId = TestIds.TenantId,
            UserId = TestIds.AdminUserId,
            UserName = "admin",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });

        await service.RevokeAsync(session.SessionId, "test revoke");

        Assert.True(await service.IsRevokedAsync(session.SessionId));
        Assert.True(repository.Items.Single().IsRevoked);
    }

    private static AppDbContext CreateDbContext()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Test");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options, tenantContext, new NullAuditContext());
    }

    private static User CreateAdmin(IPasswordHasher<User> passwordHasher, string password)
    {
        var admin = new User
        {
            Id = TestIds.AdminUserId,
            TenantId = TestIds.TenantId,
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            DisplayName = "System Administrator",
            IsEnabled = true,
            IsBuiltin = true
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        return admin;
    }
}
