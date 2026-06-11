using PermissionSystem.Application.Sso;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Sso;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Sso;

public sealed class SsoRegressionTests
{
    [Fact]
    public async Task DisabledProvider_CannotStartLogin()
    {
        var provider = CreateProvider();
        provider.Enabled = false;
        var service = new OidcClientService(
            new InMemoryRepository<SsoProvider>(provider),
            new TestConfigValueProtector(),
            new TestCacheService());

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.BuildChallengeAsync(new OidcChallengeRequest
            {
                ProviderCode = provider.ProviderCode,
                CallbackUrl = "https://app.example.test/api/sso/oidc/TEST/callback",
                ReturnUrl = "/dashboard"
            }));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
    }

    [Fact]
    public async Task Sso_ShouldNotAutomaticallyAssignSuperAdmin()
    {
        var provider = CreateProvider();
        var superAdminRole = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = SystemBuiltinConstants.SuperAdminRoleCode,
            Name = SystemBuiltinConstants.SuperAdminRoleName,
            IsEnabled = true
        };
        provider.DefaultRoleIds = superAdminRole.Id.ToString();
        var service = CreateLoginService(roles: new InMemoryRepository<Role>(superAdminRole));

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CompleteLoginAsync(provider, CreateExternalUser("external-1"), CreateContext()));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Contains("SuperAdmin", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SsoLoginCode_CanOnlyBeConsumedOnce()
    {
        var cache = new TestCacheService();
        var service = CreateLoginService(cache: cache);
        var provider = CreateProvider();

        var loginCode = await service.CompleteLoginAsync(
            provider,
            CreateExternalUser("external-1"),
            CreateContext());

        var first = await service.ConsumeLoginCodeAsync(loginCode.LoginCode);
        var second = await service.ConsumeLoginCodeAsync(loginCode.LoginCode);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    private static SsoLoginService CreateLoginService(
        InMemoryRepository<Role>? roles = null,
        TestCacheService? cache = null)
    {
        return new SsoLoginService(
            new InMemoryRepository<Tenant>(new Tenant
            {
                Id = TestIds.TenantId,
                TenantId = TestIds.TenantId,
                Code = "default",
                Name = "Default",
                IsEnabled = true
            }),
            new InMemoryRepository<User>(),
            roles ?? new InMemoryRepository<Role>(),
            new InMemoryRepository<UserRole>(),
            new InMemoryRepository<RolePermission>(),
            new InMemoryRepository<Permission>(),
            new InMemoryRepository<SsoUserBinding>(),
            new InMemoryRepository<SsoRoleMapping>(),
            new InMemoryRepository<SsoDepartmentMapping>(),
            new InMemoryRepository<Department>(),
            new InMemoryRepository<SsoLoginLog>(),
            new TestPasswordHashService(),
            cache ?? new TestCacheService(),
            new TestUnitOfWork());
    }

    private static SsoProvider CreateProvider()
    {
        return new SsoProvider
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ProviderCode = "TEST",
            ProviderName = "Test OIDC",
            ProviderType = SsoProviderType.Oidc,
            Enabled = true,
            Authority = "https://idp.example.test",
            ClientId = "client",
            Scopes = "openid profile email",
            CallbackPath = "/api/sso/oidc/callback",
            ResponseType = "code",
            UsePkce = true,
            UserIdClaim = "sub",
            UserNameClaim = "preferred_username",
            EmailClaim = "email",
            PhoneClaim = "phone_number",
            DisplayNameClaim = "name",
            RoleClaim = "roles",
            DepartmentClaim = "department",
            AutoBindUser = true,
            AutoCreateUser = true,
            AllowLocalLoginFallback = true
        };
    }

    private static ExternalSsoUser CreateExternalUser(string externalUserId)
    {
        return new ExternalSsoUser
        {
            ExternalUserId = externalUserId,
            UserName = "sso-user",
            Email = "sso-user@example.test",
            DisplayName = "SSO User",
            Claims = new Dictionary<string, string> { ["sub"] = externalUserId }
        };
    }

    private static SsoLoginContext CreateContext()
    {
        return new SsoLoginContext
        {
            IpAddress = "127.0.0.1",
            UserAgent = "xunit",
            TraceId = "trace-test"
        };
    }
}
