using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Notifications;

public sealed partial class NotificationService : IControlledNotificationService
{
    public async Task<ControlledNotificationReceipt> StageAsync(ControlledNotificationRequest request, CancellationToken ct)
    {
        ValidateControlled(request);
        if (_deliveryOptions.DeliveryMode == NotificationDeliveryMode.Disabled)
            return new(NotificationDeliveryStatuses.Disabled);
        var existing = await RequireLookup().FindAsync(request.TenantId, request.DeliveryKey, ct);
        if (existing is not null) return existing;
        if (_deliveryOptions.DeliveryMode == NotificationDeliveryMode.Direct) return await PersistAsync(request, ct);
        var messageId = await _outboxService.EnqueueAsync(NotificationMessageNames.Exchange, NotificationMessageNames.RoutingKey,
            new NotificationCreatedEvent { TenantId = request.TenantId, RecipientUserIds = [request.RecipientUserId],
                ControlledSource = request.SourceKind, ControlledSourceId = request.SourceId },
            tenantId: request.TenantId, messageId: request.DeliveryKey, cancellationToken: ct);
        return new(NotificationDeliveryStatuses.Queued, MessageId: messageId);
    }

    // The caller owns the transaction containing both the durable effect and its source receipt.
    public async Task<ControlledNotificationReceipt> PersistAsync(ControlledNotificationRequest request, CancellationToken ct)
    {
        ValidateControlled(request);
        var existing = await RequireLookup().FindAsync(request.TenantId, request.DeliveryKey, ct);
        if (existing?.NotificationId is not null) return existing;
        if (ResolveRecipients(request.TenantId, [request.RecipientUserId]).Count != 1)
            throw new BusinessException(ErrorCode.Forbidden, "Controlled notification recipient is unavailable.");
        var notification = new Notification { Id = Guid.NewGuid(), TenantId = request.TenantId,
            DeliveryKey = request.DeliveryKey, Type = NotificationTypes.Task, Title = request.Title, Content = request.Content,
            LinkUrl = request.LinkUrl, SenderName = "Demo Reminders" };
        await _notificationRepository.AddAsync(notification, ct);
        await _userNotificationRepository.AddAsync(new UserNotification { Id = Guid.NewGuid(), TenantId = request.TenantId,
            NotificationId = notification.Id, Notification = notification, UserId = request.RecipientUserId }, ct);
        return new(NotificationDeliveryStatuses.Delivered, notification.Id);
    }

    public async Task PushAsync(string deliveryKey, CancellationToken ct)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var notification = _notificationRepository.Query().FirstOrDefault(n => n.TenantId == tenantId && n.DeliveryKey == deliveryKey);
            if (notification is null) return;
            var receipt = _userNotificationRepository.Query().FirstOrDefault(n => n.TenantId == tenantId &&
                n.NotificationId == notification.Id && n.UserId == _currentUserService.UserId);
            if (receipt is not null)
                await _realtimeSender.SendToUsersAsync([receipt.UserId], ToResponseMessage(receipt, notification), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch { /* The durable inbox remains readable when realtime delivery is unavailable. */ }
    }

    private IControlledNotificationLookup RequireLookup() => _controlledLookup ??
        throw new InvalidOperationException("Controlled notification persistence is not registered.");

    private void ValidateControlled(ControlledNotificationRequest request)
    {
        if (!_currentUserService.IsAuthenticated || request.TenantId == Guid.Empty ||
            request.TenantId != _currentUserService.TenantId || request.RecipientUserId != _currentUserService.UserId ||
            request.SourceId == Guid.Empty || request.DeliveryKey.Length is < 1 or > 64 ||
            !_sourceHandlers.Any(h => h.SourceKind == request.SourceKind))
            throw new BusinessException(ErrorCode.Forbidden, "Controlled notification source and recipient are required.");
    }

    private static NotificationRealtimeMessage ToResponseMessage(UserNotification receipt, Notification notification) => new()
    { Id = receipt.Id, NotificationId = notification.Id, Type = notification.Type, Title = notification.Title,
        Content = notification.Content, LinkUrl = notification.LinkUrl, CreatedAt = receipt.CreatedAt };
}
