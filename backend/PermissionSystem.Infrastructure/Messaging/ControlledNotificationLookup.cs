using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.Infrastructure.Messaging;

public sealed class ControlledNotificationLookup(AppDbContext db) : IControlledNotificationLookup
{
    public async Task<ControlledNotificationReceipt?> FindAsync(Guid tenantId, string deliveryKey, CancellationToken ct)
    {
        if (db.IsSystemTenantScopeActive || db.CurrentTenantId != tenantId)
            throw new InvalidOperationException("Controlled notification lookup requires the current tenant.");
        var notification = await db.Notifications.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(n =>
            n.TenantId == tenantId && n.DeliveryKey == deliveryKey, ct);
        if (notification is not null) return new(NotificationDeliveryStatuses.Delivered, notification.Id);
        var message = await db.OutboxMessages.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(m =>
            m.TenantId == tenantId && m.MessageId == deliveryKey, ct);
        return message is null ? null : new(NotificationDeliveryStatuses.Queued, MessageId: message.MessageId);
    }
}
