using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Integration;
using PermissionSystem.Application.Security;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Tests;

public sealed class OpenIntegrationServiceTests
{
    private static readonly Guid DefaultTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantA = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task ValidateApiClientAsync_ShouldRejectLookupWhenTenantIsNotExplicit()
    {
        var secret = "ps_test_secret";
        var client = CreateClient(TenantB, "ERP");
        var service = CreateService(
            new TestTenantContext(DefaultTenantId, "Default"),
            clients: new InMemoryRepository<ApiClient>(client),
            secrets: new InMemoryRepository<ApiClientSecret>(CreateSecret(TenantB, client.Id, secret)));

        var result = await service.ValidateApiClientAsync("ERP", secret, "127.0.0.1");

        Assert.False(result.Succeeded);
        Assert.Contains("X-Tenant-Id", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateApiClientAsync_ShouldRequireTenantHeaderForDuplicateClientCodes()
    {
        var secret = "ps_test_secret";
        var clientA = CreateClient(TenantA, "ERP");
        var clientB = CreateClient(TenantB, "ERP");
        var clients = new InMemoryRepository<ApiClient>(clientA, clientB);
        var secrets = new InMemoryRepository<ApiClientSecret>(
            CreateSecret(TenantA, clientA.Id, secret),
            CreateSecret(TenantB, clientB.Id, secret));
        var serviceWithoutHeader = CreateService(new TestTenantContext(DefaultTenantId, "Default"), clients, secrets);

        var unresolved = await serviceWithoutHeader.ValidateApiClientAsync("ERP", secret, "127.0.0.1");

        Assert.False(unresolved.Succeeded);
        Assert.Contains("X-Tenant-Id", unresolved.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var serviceWithHeader = CreateService(new TestTenantContext(TenantB, "Header"), clients, secrets);
        var resolved = await serviceWithHeader.ValidateApiClientAsync("ERP", secret, "127.0.0.1");

        Assert.True(resolved.Succeeded);
        Assert.Equal(TenantB, resolved.TenantId);
        Assert.Equal(clientB.Id, resolved.ClientId);
    }

    [Fact]
    public async Task ValidateApiClientAsync_ShouldRejectDisabledClient()
    {
        var secret = "ps_test_secret";
        var client = CreateClient(TenantA, "ERP");
        client.IsEnabled = false;
        var service = CreateService(
            new TestTenantContext(TenantA, "Header"),
            clients: new InMemoryRepository<ApiClient>(client),
            secrets: new InMemoryRepository<ApiClientSecret>(CreateSecret(TenantA, client.Id, secret)));

        var result = await service.ValidateApiClientAsync("ERP", secret, "127.0.0.1");

        Assert.False(result.Succeeded);
        Assert.Contains("disabled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateApiClientAsync_ShouldApplyClientIpAllowListWithCidr()
    {
        var secret = "ps_test_secret";
        var client = CreateClient(TenantA, "ERP");
        client.AllowedIpList = "10.10.0.0/16;192.168.1.*";
        var service = CreateService(
            new TestTenantContext(TenantA, "Header"),
            clients: new InMemoryRepository<ApiClient>(client),
            secrets: new InMemoryRepository<ApiClientSecret>(CreateSecret(TenantA, client.Id, secret)));

        var allowed = await service.ValidateApiClientAsync("ERP", secret, "10.10.2.8");
        var rejected = await service.ValidateApiClientAsync("ERP", secret, "10.11.2.8");

        Assert.True(allowed.Succeeded);
        Assert.False(rejected.Succeeded);
        Assert.Contains("IP", rejected.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeliverWebhookAsync_ShouldStoreRedactedPayloadAndResponse()
    {
        var subscription = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            EventType = "user.created",
            TargetUrl = "https://example.test/webhook",
            Secret = "secret",
            IsEnabled = true,
            RetryCount = 0
        };
        var logs = new InMemoryRepository<WebhookDeliveryLog>();
        var service = CreateService(
            new TestTenantContext(TenantA, "Header"),
            webhooks: new InMemoryRepository<WebhookSubscription>(subscription),
            webhookLogs: logs,
            sender: new TestWebhookSender("token=abc123"));

        await service.DeliverWebhookAsync(subscription.Id, "user.created", "{\"email\":\"admin@example.com\"}", 0);

        var log = Assert.Single(logs.Items);
        Assert.StartsWith("[redacted]", log.Payload, StringComparison.Ordinal);
        Assert.StartsWith("[redacted]", log.ResponseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("admin@example.com", log.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc123", log.ResponseBody, StringComparison.OrdinalIgnoreCase);
    }

    private static OpenIntegrationService CreateService(
        TestTenantContext tenantContext,
        InMemoryRepository<ApiClient>? clients = null,
        InMemoryRepository<ApiClientSecret>? secrets = null,
        InMemoryRepository<WebhookSubscription>? webhooks = null,
        InMemoryRepository<WebhookDeliveryLog>? webhookLogs = null,
        TestWebhookSender? sender = null)
    {
        return new OpenIntegrationService(
            clients ?? new InMemoryRepository<ApiClient>(),
            secrets ?? new InMemoryRepository<ApiClientSecret>(),
            webhooks ?? new InMemoryRepository<WebhookSubscription>(),
            webhookLogs ?? new InMemoryRepository<WebhookDeliveryLog>(),
            new InMemoryRepository<ExternalApiCallLog>(),
            new TestBackgroundJobService(),
            new TestConfigValueProtector(),
            sender ?? new TestWebhookSender(),
            new TestSecurityPolicyService(),
            tenantContext,
            new TestUnitOfWork());
    }

    private static ApiClient CreateClient(Guid tenantId, string clientCode)
    {
        return new ApiClient
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClientCode = clientCode,
            ClientName = clientCode,
            IsEnabled = true,
            AllowedScopes = "report:view",
            RateLimitPerMinute = 60
        };
    }

    private static ApiClientSecret CreateSecret(Guid tenantId, Guid clientId, string secret)
    {
        return new ApiClientSecret
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClientId = clientId,
            SecretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)))
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

        public IQueryable<TEntity> Query()
        {
            return _items.Where(entity => !entity.IsDeleted).ToList().AsQueryable();
        }

        public IQueryable<TEntity> QueryForTenant(Guid tenantId)
        {
            return _items.Where(entity => !entity.IsDeleted && entity.TenantId == tenantId).ToList().AsQueryable();
        }

        public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(entity => entity.Id == id && !entity.IsDeleted));
        }

        public Task<IReadOnlyList<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TEntity>>(
                _items.Where(entity => !entity.IsDeleted).AsQueryable().Where(predicate).ToList());
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

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(Guid tenantId, string source)
        {
            TenantId = tenantId;
            Source = source;
        }

        public Guid? TenantId { get; private set; }
        public string? Source { get; private set; }
        public bool IsResolved => TenantId.HasValue;
        public bool IsSuperAdmin { get; private set; }
        public bool IsSystemScopeActive { get; private set; }
        public bool IsHttpRequest { get; private set; }

        public void SetTenant(Guid tenantId, string source)
        {
            TenantId = tenantId;
            Source = source;
        }

        public void MarkAsSuperAdmin(bool isSuperAdmin)
        {
            IsSuperAdmin = isSuperAdmin;
        }

        public void MarkAsHttpRequest()
        {
            IsHttpRequest = true;
        }
    }

    private sealed class TestBackgroundJobService : IBackgroundJobService
    {
        public string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall) => Guid.NewGuid().ToString("N");
        public string Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay) => Guid.NewGuid().ToString("N");
        public void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null, string queue = "default")
        {
        }

        public void RemoveRecurring(string recurringJobId)
        {
        }

        public void TriggerRecurring(string recurringJobId)
        {
        }

        public bool Delete(string jobId) => true;
    }

    private sealed class TestConfigValueProtector : IConfigValueProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string protectedValue) => protectedValue;
    }

    private sealed class TestSecurityPolicyService : ISecurityPolicyService
    {
        public Task<SecurityPolicyResponse> GetPolicyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SecurityPolicyResponse());
        }

        public Task<SecurityPolicyResponse> UpdatePolicyAsync(UpdateSecurityPolicyRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SecurityPolicyResponse());
        }

        public Task ValidatePasswordAsync(string password, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task EnsureLoginAllowedAsync(string userName, string? ipAddress, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RecordLoginFailureAsync(Guid tenantId, string userName, string? ipAddress, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearLoginFailureAsync(Guid tenantId, string userName, string? ipAddress, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<SendSensitiveVerificationResponse> SendVerificationAsync(SendSensitiveVerificationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SendSensitiveVerificationResponse());
        }

        public Task VerifyAsync(VerifySensitiveOperationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task EnsureSensitiveOperationVerifiedAsync(string operationCode, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task EnsureSensitiveOperationVerifiedAsync(string operationCode, bool force, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> IsIpAllowedAsync(string? ipAddress, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<PagedResult<IpAccessRuleResponse>> GetIpRulesAsync(IpAccessRuleQueryRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PagedResult<IpAccessRuleResponse>.Create([], request.PageIndex, request.PageSize, 0));
        }

        public Task<IpAccessRuleResponse> CreateIpRuleAsync(CreateIpAccessRuleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IpAccessRuleResponse());
        }

        public Task<IpAccessRuleResponse> UpdateIpRuleAsync(Guid id, UpdateIpAccessRuleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IpAccessRuleResponse());
        }

        public Task DeleteIpRuleAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<PagedResult<LoginFailureRecordResponse>> GetLoginFailuresAsync(LoginFailureQueryRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PagedResult<LoginFailureRecordResponse>.Create([], request.PageIndex, request.PageSize, 0));
        }
    }

    private sealed class TestWebhookSender : IWebhookHttpSender
    {
        private readonly string? _responseBody;

        public TestWebhookSender(string? responseBody = null)
        {
            _responseBody = responseBody;
        }

        public Task<WebhookSendResult> SendAsync(
            string targetUrl,
            string eventType,
            string payload,
            string secret,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WebhookSendResult
            {
                Succeeded = true,
                StatusCode = 200,
                ResponseBody = _responseBody
            });
        }
    }
}
