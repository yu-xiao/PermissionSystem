using Microsoft.AspNetCore.SignalR;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Api.Hubs;

[Permission(AiCenterConstants.ChatUsePermission)]
public sealed class AiHub(AiRunSubscriptions subscriptions, AiRunProgressReader reader, ITenantContext tenant) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public async Task SubscribeRun(Guid runId)
    {
        var subscription = AiRunSubscriptions.FromClaims(Context.User, runId) ?? throw new HubException("Invalid AI subscription.");
        tenant.SetTenant(subscription.TenantId, "Request");
        if (await reader.ReadAsync(subscription.TenantId, subscription.UserId, subscription.SessionId, subscription.Stamp,
            runId, Context.ConnectionAborted) is null) throw new HubException("AI Run is unavailable.");
        subscriptions.Set(Context.ConnectionId, subscription);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    { subscriptions.Remove(Context.ConnectionId); return base.OnDisconnectedAsync(exception); }
}
