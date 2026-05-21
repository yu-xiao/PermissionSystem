using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class UserNotification : BaseEntity
{
    public Guid NotificationId { get; set; }

    public Notification? Notification { get; set; }

    public Guid UserId { get; set; }

    public bool IsRead { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
