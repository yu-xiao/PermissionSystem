using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Mcp;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class McpClientAccessServiceTests
{
    [Fact]
    public async Task AdmitRequestAsync_RejectsIpOutsideBindingAllowList()
    {
        var service = CreateService("10.0.0.0/24", tenantActive: true, rateAcquired: true);

        var result = await service.AdmitRequestAsync("mcp-client", "192.168.1.10");

        Assert.False(result.Succeeded);
        Assert.Contains("IP", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdmitRequestAsync_RejectsInactiveTenant()
    {
        var service = CreateService("*", tenantActive: false, rateAcquired: true);

        var result = await service.AdmitRequestAsync("mcp-client", "127.0.0.1");

        Assert.False(result.Succeeded);
        Assert.Contains("tenant", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdmitRequestAsync_ReturnsRetryAfterWhenDistributedLimitIsExceeded()
    {
        var service = CreateService("*", tenantActive: true, rateAcquired: false);

        var result = await service.AdmitRequestAsync("mcp-client", "127.0.0.1");

        Assert.False(result.Succeeded);
        Assert.True(result.IsRateLimited);
        Assert.Equal(TimeSpan.FromSeconds(20), result.RetryAfter);
    }

    private static McpClientAccessService CreateService(
        string allowedIpList,
        bool tenantActive,
        bool rateAcquired)
    {
        return new McpClientAccessService(
            new TestBindingStore(new McpServiceClientRecord
            {
                TenantId = Guid.NewGuid(),
                ClientBindingId = Guid.NewGuid(),
                ApiClientId = Guid.NewGuid(),
                OAuthClientId = "mcp-client",
                ClientCode = "client",
                IsEnabled = true,
                AllowedScopes = string.Join(',', McpToolScopes.All),
                AllowedIpList = allowedIpList,
                RateLimitPerMinute = 60
            }),
            new TestTenantStatusChecker(tenantActive),
            new TestRateLimitService(rateAcquired));
    }

    private sealed class TestBindingStore : IMcpClientBindingStore
    {
        private readonly McpServiceClientRecord _record;

        public TestBindingStore(McpServiceClientRecord record)
        {
            _record = record;
        }

        public Task<McpServiceClientRecord?> FindByOAuthClientIdAsync(
            string oauthClientId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<McpServiceClientRecord?>(
                oauthClientId == _record.OAuthClientId ? _record : null);
        }
    }

    private sealed class TestTenantStatusChecker : ITenantStatusChecker
    {
        private readonly bool _active;

        public TestTenantStatusChecker(bool active)
        {
            _active = active;
        }

        public Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_active);
        }
    }

    private sealed class TestRateLimitService : IDistributedRateLimitService
    {
        private readonly bool _acquired;

        public TestRateLimitService(bool acquired)
        {
            _acquired = acquired;
        }

        public Task<RateLimitAcquireResult> TryAcquireAsync(
            string policyName,
            string partitionKey,
            int permitLimit,
            TimeSpan window,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RateLimitAcquireResult(
                _acquired,
                _acquired ? TimeSpan.Zero : TimeSpan.FromSeconds(20)));
        }
    }
}
