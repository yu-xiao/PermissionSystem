using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Mcp;

public sealed class McpClientBindingStore : IMcpClientBindingStore
{
    private readonly AppDbContext _dbContext;

    public McpClientBindingStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<McpServiceClientRecord?> FindByOAuthClientIdAsync(
        string oauthClientId,
        CancellationToken cancellationToken = default)
    {
        return (
            from binding in _dbContext.McpClientBindings.IgnoreQueryFilters().AsNoTracking()
            join client in _dbContext.ApiClients.IgnoreQueryFilters().AsNoTracking()
                on new { binding.TenantId, Id = binding.ApiClientId }
                equals new { client.TenantId, client.Id }
            where !binding.IsDeleted &&
                  !client.IsDeleted &&
                  binding.OAuthClientId == oauthClientId
            select new McpServiceClientRecord
            {
                TenantId = binding.TenantId,
                ClientBindingId = binding.Id,
                ApiClientId = client.Id,
                OAuthClientId = binding.OAuthClientId,
                ClientCode = client.ClientCode,
                IsEnabled = binding.IsEnabled && client.IsEnabled,
                AllowedScopes = client.AllowedScopes,
                AllowedIpList = client.AllowedIpList,
                RateLimitPerMinute = client.RateLimitPerMinute
            }).FirstOrDefaultAsync(cancellationToken);
    }
}
