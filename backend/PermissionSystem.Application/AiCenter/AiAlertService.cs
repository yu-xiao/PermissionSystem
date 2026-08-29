using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiCenter;

public interface IAiAlertService
{
    Task NotifyCircuitOpenedAsync(AiCircuitTarget target, string errorCode, CancellationToken cancellationToken = default);

    Task NotifyCircuitRecoveredAsync(AiCircuitTarget target, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class AiAlertService : IAiAlertService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IRepository<UserNotification> _userNotificationRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly IUnitOfWork _unitOfWork;

    public AiAlertService(
        IRepository<User> userRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<Role> roleRepository,
        IRepository<Notification> notificationRepository,
        IRepository<UserNotification> userNotificationRepository,
        IAsyncQueryExecutor queryExecutor,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _notificationRepository = notificationRepository;
        _userNotificationRepository = userNotificationRepository;
        _queryExecutor = queryExecutor;
        _unitOfWork = unitOfWork;
    }

    public async Task NotifyCircuitOpenedAsync(AiCircuitTarget target, string errorCode, CancellationToken cancellationToken = default)
    {
        var parts = target.Key.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var tenantId))
        {
            return;
        }

        var adminIds = await _queryExecutor.ToListAsync(
            from userRole in _userRoleRepository.QueryForTenant(tenantId)
            join role in _roleRepository.QueryForTenant(tenantId) on userRole.RoleId equals role.Id
            join user in _userRepository.QueryForTenant(tenantId) on userRole.UserId equals user.Id
            where user.IsEnabled && role.IsEnabled && role.Code == SystemBuiltinConstants.SuperAdminRoleCode
            select user.Id,
            cancellationToken);
        if (adminIds.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var notification = new Notification
        {
            TenantId = tenantId,
            Type = "Security",
            Title = "AI 组件已熔断",
            Content = $"AI {target.Kind} ({parts[1]}) 连续失败，已暂时停止调用。错误码：{errorCode}",
            SenderName = "AI Governance",
            Payload = $"{{\"target\":\"{target}\",\"errorCode\":\"{errorCode}\"}}"
        };
        await _notificationRepository.AddAsync(notification, cancellationToken);
        foreach (var userId in adminIds.Distinct())
        {
            await _userNotificationRepository.AddAsync(new UserNotification
            {
                TenantId = tenantId,
                NotificationId = notification.Id,
                UserId = userId,
                IsRead = false,
                CreatedAt = now
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task NotifyCircuitRecoveredAsync(AiCircuitTarget target, CancellationToken cancellationToken = default) =>
        NotifyAsync(target, "AI 组件已恢复", $"AI {target.Kind} ({target.Key}) 已恢复调用。", cancellationToken);

    private async Task NotifyAsync(
        AiCircuitTarget target,
        string title,
        string content,
        CancellationToken cancellationToken)
    {
        var parts = target.Key.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var tenantId)) return;
        var adminIds = await _queryExecutor.ToListAsync(
            from userRole in _userRoleRepository.QueryForTenant(tenantId)
            join role in _roleRepository.QueryForTenant(tenantId) on userRole.RoleId equals role.Id
            join user in _userRepository.QueryForTenant(tenantId) on userRole.UserId equals user.Id
            where user.IsEnabled && role.IsEnabled && role.Code == SystemBuiltinConstants.SuperAdminRoleCode
            select user.Id, cancellationToken);
        if (adminIds.Count == 0) return;
        var now = DateTimeOffset.UtcNow;
        var notification = new Notification
        {
            TenantId = tenantId,
            Type = "Security",
            Title = title,
            Content = content,
            SenderName = "AI Governance",
            Payload = $"{{\"target\":\"{target}\"}}"
        };
        await _notificationRepository.AddAsync(notification, cancellationToken);
        foreach (var userId in adminIds.Distinct())
            await _userNotificationRepository.AddAsync(new UserNotification { TenantId = tenantId, NotificationId = notification.Id, UserId = userId, IsRead = false, CreatedAt = now }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
