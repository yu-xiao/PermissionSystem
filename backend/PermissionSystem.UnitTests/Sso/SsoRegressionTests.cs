using PermissionSystem.Application.Sso;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Sso;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Sso;

public sealed class SsoRegressionTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task GlobalSsoSwitches_BlockOidcChallenge(bool enabled, bool enableOidc)
    {
        var provider = CreateProvider();
        var service = new OidcClientService(
            new InMemoryRepository<SsoProvider>(provider),
            new TestConfigValueProtector(),
            new TestCacheService(),
            new TestSsoConfiguration
            {
                Enabled = enabled,
                EnableOidc = enableOidc
            });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.BuildChallengeAsync(new OidcChallengeRequest
            {
                ProviderCode = provider.ProviderCode,
                CallbackUrl = "https://app.example.test/api/sso/oidc/TEST/callback",
                ReturnUrl = "/dashboard"
            }));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task RequireHttpsMetadata_RejectsHttpOidcAuthority()
    {
        var provider = CreateProvider();
        provider.Authority = "http://idp.example.test";
        var service = new OidcClientService(
            new InMemoryRepository<SsoProvider>(provider),
            new TestConfigValueProtector(),
            new TestCacheService(),
            new TestSsoConfiguration { RequireHttpsMetadata = true });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.BuildChallengeAsync(new OidcChallengeRequest
            {
                ProviderCode = provider.ProviderCode,
                CallbackUrl = "https://app.example.test/api/sso/oidc/TEST/callback"
            }));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
        Assert.Contains("HTTPS", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllowAutoCreateUser_DisablesProviderAutoCreation()
    {
        var provider = CreateProvider();
        var users = new InMemoryRepository<User>();
        var service = CreateLoginService(
            users: users,
            configuration: new TestSsoConfiguration { AllowAutoCreateUser = false });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CompleteLoginAsync(provider, CreateExternalUser("external-no-create"), CreateContext()));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Empty(users.Items);
    }

    [Fact]
    public async Task DisabledSso_InvalidatesLoginCodeConsumption()
    {
        var cache = new TestCacheService();
        var enabledService = CreateLoginService(cache: cache);
        var loginCode = await enabledService.CompleteLoginAsync(
            CreateProvider(),
            CreateExternalUser("external-disabled-consume"),
            CreateContext());

        var disabledService = CreateLoginService(
            cache: cache,
            configuration: new TestSsoConfiguration { Enabled = false });

        Assert.Null(await disabledService.ConsumeLoginCodeAsync(loginCode.LoginCode));

        var reenabledService = CreateLoginService(cache: cache);
        Assert.Null(await reenabledService.ConsumeLoginCodeAsync(loginCode.LoginCode));
    }

    [Fact]
    public async Task ProviderConfigurationSwitches_ApplyOnCreate()
    {
        var providers = new InMemoryRepository<SsoProvider>();
        var service = new SsoProviderService(
            providers,
            new InMemoryRepository<SsoUserBinding>(),
            new TestConfigValueProtector(),
            new TestTenantWriteResolver(),
            new TestUnitOfWork(),
            new TestSsoConfiguration
            {
                DefaultCallbackPath = "/custom/sso/callback",
                EncryptClientSecret = false
            });

        await service.CreateAsync(new CreateSsoProviderRequest
        {
            TenantId = TestIds.TenantId,
            ProviderCode = "custom",
            ProviderName = "Custom OIDC",
            Authority = "https://idp.example.test",
            ClientId = "client",
            ClientSecret = "plain-secret",
            AutoCreateUser = true,
            AllowLocalLoginFallback = true
        });

        var provider = Assert.Single(providers.Items);
        Assert.Equal("/custom/sso/callback", provider.CallbackPath);
        Assert.Equal("plain-secret", provider.ClientSecretEncrypted);
    }

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
        TestCacheService? cache = null,
        InMemoryRepository<User>? users = null,
        ISsoConfiguration? configuration = null)
    {
        return new SsoLoginService(
            new InMemoryRepository<Tenant>(new Tenant
            {
                Id = TestIds.TenantId,
                TenantId = TestIds.TenantId,
                Code = "default",
                Name = "Default",
                Status = TenantStatus.Active
            }),
            users ?? new InMemoryRepository<User>(),
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
            new TestUnitOfWork(),
            CreateTenantContext(),
            configuration);
    }

    private static TenantContext CreateTenantContext()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Test");
        return tenantContext;
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

    private sealed class TestSsoConfiguration : ISsoConfiguration
    {
        public bool Enabled { get; init; } = true;

        public bool EnableOidc { get; init; } = true;

        public bool EnableSaml { get; init; }

        public string DefaultCallbackPath { get; init; } = "/api/sso/oidc/callback";

        public bool RequireHttpsMetadata { get; init; }

        public bool EncryptClientSecret { get; init; } = true;

        public bool AllowAutoCreateUser { get; init; } = true;

        public bool AllowLocalLoginFallback { get; init; } = true;
    }
}
