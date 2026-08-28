using Microsoft.AspNetCore.SignalR;
using PermissionSystem.Api.Hubs;
using PermissionSystem.Application.AiCenter;

namespace PermissionSystem.Api.Services;

public sealed class SignalRAiRunRealtimeSender : IAiRunRealtimeSender
{
    private readonly IHubContext<AiHub> _hubContext;

    public SignalRAiRunRealtimeSender(IHubContext<AiHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendToUserAsync(
        Guid userId,
        AiRunRealtimeMessage message,
        CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients
            .Group(AiHub.GetUserGroup(userId))
            .SendAsync("ReceiveAiRunEvent", message, cancellationToken);
    }
}
