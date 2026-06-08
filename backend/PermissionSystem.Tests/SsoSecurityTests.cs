using System.Linq.Expressions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Sso;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Sso;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Tests;

public sealed class SsoSecurityTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProviderId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task BuildChallengeAsync_ShouldRejectDisabledProvider()
    {
        var provider = CreateProvider();
        provider.Enabled = false;
        var service = new OidcClientService(
            new InMemoryRepository<SsoProvider>(provider),
            new TestConfigValueProtector(),
            new TestCacheService());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.BuildChallengeAsync(new OidcChallengeRequest
        {
            ProviderCode = provider.ProviderCode,
            CallbackUrl = "http://localhost/api/sso/oidc/TEST/callback",
            ReturnUrl = "/dashboard"
        }));

        Assert.Contains("provider", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConsumeLoginCodeAsync_ShouldUseLoginCodeOnlyOnce()
    {
        var cache = new TestCacheService();
        var users = new InMemoryRepository<User>();
        var service = CreateLoginService(users: users, cache: cache);
        var provider = CreateProvider();

        var loginCode = await service.CompleteLoginAsync(
            provider,
            CreateExternalUser("external-1"),
            CreateLoginContext());

        var first = await service.ConsumeLoginCodeAsync(loginCode.LoginCode);
        var second = await service.ConsumeLoginCodeAsync(loginCode.LoginCode);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task CompleteLoginAsync_ShouldRejectDisabledLocalUser()
    {
        var user = CreateUser(isEnabled: false);
        var binding = new SsoUserBinding
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ProviderId = ProviderId,
            ProviderCode = "TEST",
            ExternalUserId = "external-1",
            LocalUserId = user.Id
        };
        var loginLogs = new InMemoryRepository<SsoLoginLog>();
        var service = CreateLoginService(
            users: new InMemoryRepository<User>(user),
            bindings: new InMemoryRepository<SsoUserBinding>(binding),
            loginLogs: loginLogs);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CompleteLoginAsync(
            CreateProvider(),
            CreateExternalUser("external-1"),
            CreateLoginContext()));

        Assert.Contains("disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SsoLoginResult.UserDisabled, Assert.Single(loginLogs.Items).LoginResult);
    }

    [Fact]
    public async Task CompleteLoginAsync_ShouldRejectSuperAdminDefaultRole()
    {
        var superAdminRole = CreateRole(SystemBuiltinConstants.SuperAdminRoleCode);
        var provider = CreateProvider();
        provider.DefaultRoleIds = superAdminRole.Id.ToString();
        var service = CreateLoginService(roles: new InMemoryRepository<Role>(superAdminRole));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CompleteLoginAsync(
            provider,
            CreateExternalUser("external-1"),
            CreateLoginContext()));

        Assert.Contains("SuperAdmin", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveRoleMappingsAsync_ShouldRejectSuperAdminMapping()
    {
        var provider = CreateProvider();
        var superAdminRole = CreateRole(SystemBuiltinConstants.SuperAdminRoleCode);
        var service = new SsoManagementService(
            new InMemoryRepository<SsoProvider>(provider),
            new InMemoryRepository<SsoUserBinding>(),
            new InMemoryRepository<SsoRoleMapping>(),
            new InMemoryRepository<SsoDepartmentMapping>(),
            new InMemoryRepository<SsoLoginLog>(),
            new InMemoryRepository<User>(),
            new InMemoryRepository<Role>(superAdminRole),
            new InMemoryRepository<Department>(),
            new TestUnitOfWork());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SaveRoleMappingsAsync(
            provider.Id,
            [new SsoRoleMappingRequest { ExternalRole = "admins", LocalRoleId = superAdminRole.Id }]));

        Assert.Contains("SuperAdmin", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SsoLoginService CreateLoginService(
        InMemoryRepository<User>? users = null,
        InMemoryRepository<Role>? roles = null,
        InMemoryRepository<SsoUserBinding>? bindings = null,
        InMemoryRepository<SsoLoginLog>? loginLogs = null,
        TestCacheService? cache = null)
    {
        return new SsoLoginService(
            new InMemoryRepository<Tenant>(new Tenant { Id = TenantId, TenantId = TenantId, Code = "default", Name = "Default", IsEnabled = true }),
            users ?? new InMemoryRepository<User>(),
            roles ?? new InMemoryRepository<Role>(),
            new InMemoryRepository<UserRole>(),
            new InMemoryRepository<RolePermission>(),
            new InMemoryRepository<Permission>(),
            bindings ?? new InMemoryRepository<SsoUserBinding>(),
            new InMemoryRepository<SsoRoleMapping>(),
            new InMemoryRepository<SsoDepartmentMapping>(),
            new InMemoryRepository<Department>(),
            loginLogs ?? new InMemoryRepository<SsoLoginLog>(),
            new TestPasswordHashService(),
            cache ?? new TestCacheService(),
            new TestUnitOfWork());
    }

    private static SsoProvider CreateProvider()
    {
        return new SsoProvider
        {
            Id = ProviderId,
            TenantId = TenantId,
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

    private static User CreateUser(bool isEnabled = true)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserName = "local-user",
            NormalizedUserName = "LOCAL-USER",
            DisplayName = "Local User",
            IsEnabled = isEnabled
        };
    }

    private static Role CreateRole(string code)
    {
        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Code = code,
            Name = code,
            IsEnabled = true
        };
    }

    private static SsoLoginContext CreateLoginContext()
    {
        return new SsoLoginContext
        {
            IpAddress = "127.0.0.1",
            UserAgent = "xunit",
            TraceId = "trace-test"
        };
    }

    private sealed class InMemoryRepository<TEntity> : IRepository<TEntity>
        where TEntity : BaseEntity
    {
        private readonly List<TEntity> _items;

        public InMemoryRepository(params TEntity[] items)
        {
            _items = items.ToList();
        }

        public IReadOnlyList<TEntity> Items => _items;

        public IQueryable<TEntity> Query(bool ignoreQueryFilters = false)
        {
            return _items.Where(entity => !entity.IsDeleted).ToList().AsQueryable();
        }

        public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(entity => entity.Id == id && !entity.IsDeleted));
        }

        public Task<IReadOnlyList<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TEntity>>(_items.Where(entity => !entity.IsDeleted).AsQueryable().Where(predicate).ToList());
        }

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }

            _items.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(TEntity entity)
        {
        }

        public void Remove(TEntity entity)
        {
            entity.IsDeleted = true;
        }
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            return action(cancellationToken);
        }
    }

    private sealed class TestPasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password)
        {
            return "hashed:" + password;
        }

        public bool VerifyPassword(string passwordHash, string password)
        {
            return passwordHash == "hashed:" + password;
        }
    }

    private sealed class TestConfigValueProtector : IConfigValueProtector
    {
        public string Protect(string value)
        {
            return "protected:" + value;
        }

        public string Unprotect(string protectedValue)
        {
            return protectedValue.StartsWith("protected:", StringComparison.Ordinal)
                ? protectedValue["protected:".Length..]
                : protectedValue;
        }
    }

    private sealed class TestCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _items = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.TryGetValue(key, out var value) ? (T)value : default);
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.ContainsKey(key));
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? absoluteExpirationRelativeToNow = null,
            TimeSpan? slidingExpiration = null,
            CancellationToken cancellationToken = default)
        {
            _items[key] = value!;
            return Task.CompletedTask;
        }

        public async Task<T> GetOrSetAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? absoluteExpirationRelativeToNow = null,
            TimeSpan? slidingExpiration = null,
            CancellationToken cancellationToken = default)
        {
            if (_items.TryGetValue(key, out var value))
            {
                return (T)value;
            }

            var created = await factory(cancellationToken);
            _items[key] = created!;
            return created;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _items.Remove(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            foreach (var key in _items.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            {
                _items.Remove(key);
            }

            return Task.CompletedTask;
        }

        public Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
