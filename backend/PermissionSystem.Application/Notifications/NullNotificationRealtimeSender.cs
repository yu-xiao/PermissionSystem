namespace PermissionSystem.Application.Notifications;

public sealed class NullNotificationRealtimeSender : INotificationRealtimeSender
{
    public Task SendToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        NotificationRealtimeMessage message,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
