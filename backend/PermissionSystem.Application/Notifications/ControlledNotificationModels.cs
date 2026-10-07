namespace PermissionSystem.Application.Notifications;

public sealed record ControlledNotificationRequest(Guid TenantId, Guid RecipientUserId, string DeliveryKey,
    string SourceKind, Guid SourceId, string Title, string Content, string LinkUrl);
public sealed record ControlledNotificationReceipt(string Status, Guid? NotificationId = null, string? MessageId = null);

public interface IControlledNotificationService
{
    Task<ControlledNotificationReceipt> StageAsync(ControlledNotificationRequest request, CancellationToken ct);
    Task<ControlledNotificationReceipt> PersistAsync(ControlledNotificationRequest request, CancellationToken ct);
    Task PushAsync(string deliveryKey, CancellationToken ct);
}

public interface IControlledNotificationLookup
{
    Task<ControlledNotificationReceipt?> FindAsync(Guid tenantId, string deliveryKey, CancellationToken ct);
}

public interface INotificationSourceHandler
{
    string SourceKind { get; }
    Task HandleAsync(Guid tenantId, Guid sourceId, CancellationToken ct);
}
