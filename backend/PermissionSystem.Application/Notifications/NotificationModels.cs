using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Notifications;

public static class NotificationTypes
{
    public const string System = "System";

    public const string Security = "Security";

    public const string Task = "Task";

    public const string Approval = "Approval";
}

public static class NotificationTemplateStatuses
{
    public const string Enabled = "Enabled";

    public const string Disabled = "Disabled";
}

public static class NotificationMessageNames
{
    public const string Exchange = "permission-system.exchange";

    public const string RoutingKey = "notifications.created";

    public const string QueueName = "permission-system.notifications";
}

public sealed class NotificationQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? Type { get; init; }

    public bool? IsRead { get; init; }
}

public sealed class NotificationResponse
{
    public Guid Id { get; init; }

    public Guid NotificationId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string? SenderName { get; init; }

    public string? LinkUrl { get; init; }

    public string? Payload { get; init; }

    public bool IsRead { get; init; }

    public DateTimeOffset? ReadAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class NotificationRealtimeMessage
{
    public Guid Id { get; init; }

    public Guid NotificationId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string? LinkUrl { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class SendSystemNotificationRequest
{
    public Guid? TenantId { get; init; }

    public IReadOnlyCollection<Guid>? RecipientUserIds { get; init; }

    public string Type { get; init; } = NotificationTypes.System;

    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string? LinkUrl { get; init; }

    public string? Payload { get; init; }
}

public sealed class NotificationTemplateQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? Type { get; init; }

    public string? Status { get; init; }
}

public sealed class SaveNotificationTemplateRequest
{
    public Guid? TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = NotificationTypes.System;

    public string TitleTemplate { get; init; } = string.Empty;

    public string ContentTemplate { get; init; } = string.Empty;

    public string Status { get; init; } = NotificationTemplateStatuses.Enabled;

    public int Sort { get; init; }

    public string? Remark { get; init; }
}

public sealed class NotificationTemplateResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string TitleTemplate { get; init; } = string.Empty;

    public string ContentTemplate { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int Sort { get; init; }

    public string? Remark { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class NotificationCreatedEvent
{
    public Guid? TenantId { get; init; }

    public IReadOnlyCollection<Guid>? RecipientUserIds { get; init; }

    public string Type { get; init; } = NotificationTypes.System;

    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string? LinkUrl { get; init; }

    public string? Payload { get; init; }
}

public interface INotificationRealtimeSender
{
    Task SendToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        NotificationRealtimeMessage message,
        CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task<PagedResult<NotificationResponse>> GetMyNotificationsAsync(NotificationQueryRequest request, CancellationToken cancellationToken = default);

    Task<int> GetMyUnreadCountAsync(CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(CancellationToken cancellationToken = default);

    Task DeleteMineAsync(Guid id, CancellationToken cancellationToken = default);

    Task SendSystemNotificationAsync(SendSystemNotificationRequest request, CancellationToken cancellationToken = default);

    Task HandleNotificationEventAsync(NotificationCreatedEvent notificationEvent, CancellationToken cancellationToken = default);

    Task<PagedResult<NotificationTemplateResponse>> GetTemplatesAsync(NotificationTemplateQueryRequest request, CancellationToken cancellationToken = default);

    Task<NotificationTemplateResponse> CreateTemplateAsync(SaveNotificationTemplateRequest request, CancellationToken cancellationToken = default);

    Task<NotificationTemplateResponse> UpdateTemplateAsync(Guid id, SaveNotificationTemplateRequest request, CancellationToken cancellationToken = default);

    Task DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default);
}
