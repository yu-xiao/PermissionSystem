using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class Notification : BaseEntity
{
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public Guid? SenderId { get; set; }

    public string? SenderName { get; set; }

    public string? LinkUrl { get; set; }

    public string? Payload { get; set; }

    public ICollection<UserNotification> UserNotifications { get; set; } = [];
}
