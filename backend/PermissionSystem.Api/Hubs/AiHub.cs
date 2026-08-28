using Microsoft.AspNetCore.SignalR;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Hubs;

[Permission(AiCenterConstants.ChatUsePermission)]
public sealed class AiHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimConstants.UserId)?.Value;
        if (Guid.TryParse(userId, out var parsedUserId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(parsedUserId));
        }

        await base.OnConnectedAsync();
    }

    public static string GetUserGroup(Guid userId) => $"ai-user:{userId}";
}
