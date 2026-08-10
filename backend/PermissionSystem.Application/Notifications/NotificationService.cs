using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Notifications;

public sealed class NotificationService : INotificationService
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        NotificationTypes.System,
        NotificationTypes.Security,
        NotificationTypes.Task,
        NotificationTypes.Approval
    };

    private readonly IRepository<Notification> _notificationRepository;
    private readonly IRepository<UserNotification> _userNotificationRepository;
    private readonly IRepository<NotificationTemplate> _templateRepository;
    private readonly IRepository<User> _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IOutboxService _outboxService;
    private readonly INotificationRealtimeSender _realtimeSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NotificationDeliveryOptions _deliveryOptions;

    public NotificationService(
        IRepository<Notification> notificationRepository,
        IRepository<UserNotification> userNotificationRepository,
        IRepository<NotificationTemplate> templateRepository,
        IRepository<User> userRepository,
        ICurrentUserService currentUserService,
        ITenantWriteResolver tenantWriteResolver,
        IOutboxService outboxService,
        INotificationRealtimeSender realtimeSender,
        IUnitOfWork unitOfWork,
        NotificationDeliveryOptions deliveryOptions)
    {
        _notificationRepository = notificationRepository;
        _userNotificationRepository = userNotificationRepository;
        _templateRepository = templateRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _tenantWriteResolver = tenantWriteResolver;
        _outboxService = outboxService;
        _realtimeSender = realtimeSender;
        _unitOfWork = unitOfWork;
        _deliveryOptions = deliveryOptions;
    }

    public NotificationDeliveryStatusResponse GetDeliveryStatus()
    {
        return new NotificationDeliveryStatusResponse
        {
            Mode = _deliveryOptions.DeliveryMode.ToString(),
            IsEnabled = _deliveryOptions.DeliveryMode != NotificationDeliveryMode.Disabled,
            Description = _deliveryOptions.DeliveryMode switch
            {
                NotificationDeliveryMode.Direct => "Notifications are persisted directly.",
                NotificationDeliveryMode.OutboxRabbitMQ => "Notifications are queued through Outbox and RabbitMQ.",
                _ => "Notification delivery is disabled."
            }
        };
    }

    public Task<PagedResult<NotificationResponse>> GetMyNotificationsAsync(
        NotificationQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var query = _userNotificationRepository.Query()
            .Where(entity => entity.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = request.Type.Trim();
            query = query.Where(entity => entity.Notification != null && entity.Notification.Type == type);
        }

        if (request.IsRead.HasValue)
        {
            query = query.Where(entity => entity.IsRead == request.IsRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.Notification != null &&
                (entity.Notification.Title.Contains(keyword) ||
                    entity.Notification.Content.Contains(keyword)));
        }

        var totalCount = query.LongCount();
        var rows = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList();
        var notificationIds = rows.Select(entity => entity.NotificationId).Distinct().ToArray();
        var notifications = _notificationRepository.Query()
            .Where(entity => notificationIds.Contains(entity.Id))
            .ToDictionary(entity => entity.Id);
        var items = rows
            .Select(entity => ToResponse(entity, notifications.GetValueOrDefault(entity.NotificationId)))
            .ToList();

        return Task.FromResult(PagedResult<NotificationResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public Task<int> GetMyUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var count = _userNotificationRepository.Query()
            .Count(entity => entity.UserId == userId && !entity.IsRead);

        return Task.FromResult(count);
    }

    public async Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetMineOrThrowAsync(id, cancellationToken);
        if (!entity.IsRead)
        {
            entity.IsRead = true;
            entity.ReadAt = DateTimeOffset.UtcNow;
            _userNotificationRepository.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var unread = _userNotificationRepository.Query()
            .Where(entity => entity.UserId == userId && !entity.IsRead)
            .ToList();
        var now = DateTimeOffset.UtcNow;

        foreach (var item in unread)
        {
            item.IsRead = true;
            item.ReadAt = now;
            _userNotificationRepository.Update(item);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMineAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetMineOrThrowAsync(id, cancellationToken);
        _userNotificationRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationDeliveryResult> SendSystemNotificationAsync(
        SendSystemNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        ValidateType(request.Type);
        var notificationEvent = new NotificationCreatedEvent
        {
            TenantId = tenantId,
            RecipientUserIds = request.RecipientUserIds,
            Type = request.Type.Trim(),
            Title = TrimRequired(request.Title, "Notification title is required."),
            Content = TrimRequired(request.Content, "Notification content is required."),
            LinkUrl = request.LinkUrl,
            Payload = request.Payload
        };

        if (_deliveryOptions.DeliveryMode == NotificationDeliveryMode.Disabled)
        {
            return new NotificationDeliveryResult
            {
                Mode = NotificationDeliveryMode.Disabled.ToString(),
                Status = NotificationDeliveryStatuses.Disabled
            };
        }

        if (_deliveryOptions.DeliveryMode == NotificationDeliveryMode.Direct)
        {
            var userIds = ResolveRecipients(tenantId, notificationEvent.RecipientUserIds);
            var notification = await CreateNotificationAsync(
                tenantId,
                userIds,
                notificationEvent.Type,
                notificationEvent.Title,
                notificationEvent.Content,
                notificationEvent.LinkUrl,
                notificationEvent.Payload,
                null,
                NotificationDeliveryMode.Direct.ToString(),
                cancellationToken);

            return new NotificationDeliveryResult
            {
                Mode = NotificationDeliveryMode.Direct.ToString(),
                Status = NotificationDeliveryStatuses.Delivered,
                NotificationId = notification.Id
            };
        }

        var messageId = await _outboxService.EnqueueAsync(
            NotificationMessageNames.Exchange,
            NotificationMessageNames.RoutingKey,
            notificationEvent,
            tenantId: tenantId,
            cancellationToken: cancellationToken);

        return new NotificationDeliveryResult
        {
            Mode = NotificationDeliveryMode.OutboxRabbitMQ.ToString(),
            Status = NotificationDeliveryStatuses.Queued,
            MessageId = messageId
        };
    }

    public async Task HandleNotificationEventAsync(
        NotificationCreatedEvent notificationEvent,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId(notificationEvent.TenantId);
        var userIds = ResolveRecipients(tenantId, notificationEvent.RecipientUserIds);
        await CreateNotificationAsync(
            tenantId,
            userIds,
            notificationEvent.Type,
            notificationEvent.Title,
            notificationEvent.Content,
            notificationEvent.LinkUrl,
            notificationEvent.Payload,
            null,
            NotificationDeliveryMode.OutboxRabbitMQ.ToString(),
            cancellationToken);
    }

    public Task<PagedResult<NotificationTemplateResponse>> GetTemplatesAsync(
        NotificationTemplateQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _templateRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.Code.Contains(keyword) ||
                entity.Name.Contains(keyword) ||
                entity.TitleTemplate.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = request.Type.Trim();
            query = query.Where(entity => entity.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(entity => entity.Status == status);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.Code)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToTemplateResponse)
            .ToList();

        return Task.FromResult(PagedResult<NotificationTemplateResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<NotificationTemplateResponse> CreateTemplateAsync(
        SaveNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        var code = TrimRequired(request.Code, "Template code is required.");
        ValidateType(request.Type);

        if (_templateRepository.Query().Any(entity => entity.TenantId == tenantId && entity.Code == code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Notification template code already exists.");
        }

        var entity = new NotificationTemplate
        {
            TenantId = tenantId,
            Code = code,
            Name = TrimRequired(request.Name, "Template name is required."),
            Type = request.Type.Trim(),
            TitleTemplate = TrimRequired(request.TitleTemplate, "Title template is required."),
            ContentTemplate = TrimRequired(request.ContentTemplate, "Content template is required."),
            Status = NormalizeStatus(request.Status),
            Sort = request.Sort,
            Remark = request.Remark
        };

        await _templateRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToTemplateResponse(entity);
    }

    public async Task<NotificationTemplateResponse> UpdateTemplateAsync(
        Guid id,
        SaveNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _templateRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Notification template was not found.");
        ConcurrencyTokenGuard.EnsureMatches(entity, request.ConcurrencyToken);
        ValidateType(request.Type);

        entity.Name = TrimRequired(request.Name, "Template name is required.");
        entity.Type = request.Type.Trim();
        entity.TitleTemplate = TrimRequired(request.TitleTemplate, "Title template is required.");
        entity.ContentTemplate = TrimRequired(request.ContentTemplate, "Content template is required.");
        entity.Status = NormalizeStatus(request.Status);
        entity.Sort = request.Sort;
        entity.Remark = request.Remark;

        _templateRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToTemplateResponse(entity);
    }

    public async Task DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _templateRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Notification template was not found.");
        _templateRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Notification> CreateNotificationAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        string type,
        string title,
        string content,
        string? linkUrl,
        string? payload,
        Guid? senderId,
        string? senderName,
        CancellationToken cancellationToken)
    {
        ValidateType(type);
        if (userIds.Count == 0)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Notification recipients are required.");
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Type = type.Trim(),
            Title = TrimRequired(title, "Notification title is required."),
            Content = TrimRequired(content, "Notification content is required."),
            LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(),
            Payload = string.IsNullOrWhiteSpace(payload) ? null : payload.Trim(),
            SenderId = senderId,
            SenderName = senderName
        };

        await _notificationRepository.AddAsync(notification, cancellationToken);

        List<UserNotification> userNotifications = [];
        foreach (var userId in userIds.Distinct())
        {
            var userNotification = new UserNotification
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                NotificationId = notification.Id,
                Notification = notification,
                UserId = userId,
                IsRead = false
            };
            userNotifications.Add(userNotification);
            await _userNotificationRepository.AddAsync(userNotification, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var userNotification in userNotifications)
        {
            await _realtimeSender.SendToUsersAsync(
                [userNotification.UserId],
                ToRealtimeMessage(userNotification),
                cancellationToken);
        }

        return notification;
    }

    private IReadOnlyCollection<Guid> ResolveRecipients(Guid tenantId, IReadOnlyCollection<Guid>? requestedUserIds)
    {
        var query = _userRepository.Query()
            .Where(entity => entity.TenantId == tenantId && entity.IsEnabled);

        if (requestedUserIds is { Count: > 0 })
        {
            var ids = requestedUserIds.Distinct().ToArray();
            query = query.Where(entity => ids.Contains(entity.Id));
        }

        return query.Select(entity => entity.Id).ToArray();
    }

    private Guid ResolveTenantId(Guid? requestedTenantId)
    {
        return requestedTenantId
            ?? _currentUserService.TenantId
            ?? throw new BusinessException(ErrorCode.ValidationFailed, "TenantId is required.");
    }

    private Guid RequireUserId()
    {
        return _currentUserService.UserId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "User is not authenticated.");
    }

    private async Task<UserNotification> GetMineOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var entity = await _userNotificationRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.UserId != userId)
        {
            throw new BusinessException(ErrorCode.NotFound, "Notification was not found.");
        }

        return entity;
    }

    private static NotificationResponse ToResponse(UserNotification entity, Notification? notification)
    {
        return new NotificationResponse
        {
            Id = entity.Id,
            NotificationId = entity.NotificationId,
            Type = notification?.Type ?? string.Empty,
            Title = notification?.Title ?? string.Empty,
            Content = notification?.Content ?? string.Empty,
            SenderName = notification?.SenderName,
            LinkUrl = notification?.LinkUrl,
            Payload = notification?.Payload,
            IsRead = entity.IsRead,
            ReadAt = entity.ReadAt,
            CreatedAt = entity.CreatedAt
        };
    }

    private static NotificationRealtimeMessage ToRealtimeMessage(UserNotification entity)
    {
        var notification = entity.Notification!;
        return new NotificationRealtimeMessage
        {
            Id = entity.Id,
            NotificationId = entity.NotificationId,
            Type = notification.Type,
            Title = notification.Title,
            Content = notification.Content,
            LinkUrl = notification.LinkUrl,
            CreatedAt = entity.CreatedAt
        };
    }

    private static NotificationTemplateResponse ToTemplateResponse(NotificationTemplate entity)
    {
        return new NotificationTemplateResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            Name = entity.Name,
            Type = entity.Type,
            TitleTemplate = entity.TitleTemplate,
            ContentTemplate = entity.ContentTemplate,
            Status = entity.Status,
            Sort = entity.Sort,
            Remark = entity.Remark,
            CreatedAt = entity.CreatedAt,
            ConcurrencyToken = entity.RowVersion
        };
    }

    private static void ValidateType(string type)
    {
        if (string.IsNullOrWhiteSpace(type) || !SupportedTypes.Contains(type.Trim()))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Unsupported notification type.");
        }
    }

    private static string NormalizeStatus(string? status)
    {
        return string.Equals(status, NotificationTemplateStatuses.Disabled, StringComparison.OrdinalIgnoreCase)
            ? NotificationTemplateStatuses.Disabled
            : NotificationTemplateStatuses.Enabled;
    }

    private static string TrimRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }
}
