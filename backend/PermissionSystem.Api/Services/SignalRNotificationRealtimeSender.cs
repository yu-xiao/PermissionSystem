using Microsoft.AspNetCore.SignalR;
using PermissionSystem.Api.Hubs;
using PermissionSystem.Application.Notifications;

namespace PermissionSystem.Api.Services;

public sealed class SignalRNotificationRealtimeSender : INotificationRealtimeSender
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationRealtimeSender(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        NotificationRealtimeMessage message,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds.Distinct())
        {
            await _hubContext.Clients
                .Group(NotificationHub.GetUserGroup(userId))
                .SendAsync("ReceiveNotification", message, cancellationToken);
        }
    }
}
