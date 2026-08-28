using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Security;

namespace PermissionSystem.Application.Mcp;

public sealed class McpClientAccessService : IMcpClientAccessService
{
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    private readonly IMcpClientBindingStore _bindingStore;
    private readonly ITenantStatusChecker _tenantStatusChecker;
    private readonly IDistributedRateLimitService _rateLimitService;

    public McpClientAccessService(
        IMcpClientBindingStore bindingStore,
        ITenantStatusChecker tenantStatusChecker,
        IDistributedRateLimitService rateLimitService)
    {
        _bindingStore = bindingStore;
        _tenantStatusChecker = tenantStatusChecker;
        _rateLimitService = rateLimitService;
    }

    public Task<McpCallerAdmissionResult> ValidateTokenRequestAsync(
        string oauthClientId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        return ValidateAsync(oauthClientId, ipAddress, consumeRateLimit: false, cancellationToken);
    }

    public Task<McpCallerAdmissionResult> AdmitRequestAsync(
        string oauthClientId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        return ValidateAsync(oauthClientId, ipAddress, consumeRateLimit: true, cancellationToken);
    }

    private async Task<McpCallerAdmissionResult> ValidateAsync(
        string oauthClientId,
        string? ipAddress,
        bool consumeRateLimit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(oauthClientId))
        {
            return Failed("The MCP client identity is missing.");
        }

        var client = await _bindingStore.FindByOAuthClientIdAsync(oauthClientId.Trim(), cancellationToken);
        if (client is null || !client.IsEnabled)
        {
            return Failed("The MCP client is invalid or disabled.");
        }

        if (!await _tenantStatusChecker.IsActiveAsync(client.TenantId, cancellationToken))
        {
            return Failed("The MCP client tenant is not active.");
        }

        if (!IpAccessMatcher.AnyMatches(client.AllowedIpList, ipAddress))
        {
            return Failed("Current IP is not allowed for this MCP client.");
        }

        if (consumeRateLimit)
        {
            var acquired = await _rateLimitService.TryAcquireAsync(
                "mcp-client",
                $"{client.TenantId:N}:{client.ClientBindingId:N}",
                client.RateLimitPerMinute,
                RateLimitWindow,
                cancellationToken);
            if (!acquired.IsAcquired)
            {
                return new McpCallerAdmissionResult
                {
                    IsRateLimited = true,
                    RetryAfter = acquired.RetryAfter,
                    ErrorMessage = "The MCP client rate limit was exceeded.",
                    Client = client
                };
            }
        }

        return new McpCallerAdmissionResult
        {
            Succeeded = true,
            Client = client
        };
    }

    private static McpCallerAdmissionResult Failed(string message) => new()
    {
        ErrorMessage = message
    };
}
