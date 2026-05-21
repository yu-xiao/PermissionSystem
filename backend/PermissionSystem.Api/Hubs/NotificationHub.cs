using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimConstants.UserId)?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    public static string GetUserGroup(Guid userId)
    {
        return GetUserGroup(userId.ToString());
    }

    private static string GetUserGroup(string userId)
    {
        return $"user:{userId}";
    }
}
