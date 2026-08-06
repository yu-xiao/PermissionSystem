using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Authorize]
[Route("api/notifications")]
public sealed class NotificationController : ApiControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("admin/delivery-status")]
    [Permission("system:notification:send")]
    public ActionResult<ApiResult<NotificationDeliveryStatusResponse>> GetDeliveryStatus()
    {
        return Success(_notificationService.GetDeliveryStatus());
    }

    [HttpGet("my")]
    [Permission("system:notification:view")]
    public async Task<ActionResult<ApiResult<PagedResult<NotificationResponse>>>> GetMyNotificationsAsync(
        [FromQuery] NotificationQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _notificationService.GetMyNotificationsAsync(request, cancellationToken));
    }

    [HttpGet("my/unread-count")]
    [Permission("system:notification:view")]
    public async Task<ActionResult<ApiResult<int>>> GetMyUnreadCountAsync(CancellationToken cancellationToken)
    {
        return Success(await _notificationService.GetMyUnreadCountAsync(cancellationToken));
    }

    [HttpPost("my/{id:guid}/read")]
    [Permission("system:notification:view")]
    public async Task<ActionResult<ApiResult>> MarkAsReadAsync(Guid id, CancellationToken cancellationToken)
    {
        await _notificationService.MarkAsReadAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("my/read-all")]
    [Permission("system:notification:view")]
    public async Task<ActionResult<ApiResult>> MarkAllAsReadAsync(CancellationToken cancellationToken)
    {
        await _notificationService.MarkAllAsReadAsync(cancellationToken);
        return Success();
    }

    [HttpDelete("my/{id:guid}")]
    [Permission("system:notification:view")]
    public async Task<ActionResult<ApiResult>> DeleteMineAsync(Guid id, CancellationToken cancellationToken)
    {
        await _notificationService.DeleteMineAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("admin/send")]
    [Permission("system:notification:send")]
    public async Task<ActionResult<ApiResult<NotificationDeliveryResult>>> SendSystemNotificationAsync(
        [FromBody] SendSystemNotificationRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _notificationService.SendSystemNotificationAsync(request, cancellationToken));
    }

    [HttpGet("templates")]
    [Permission("system:notification-template:view")]
    public async Task<ActionResult<ApiResult<PagedResult<NotificationTemplateResponse>>>> GetTemplatesAsync(
        [FromQuery] NotificationTemplateQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _notificationService.GetTemplatesAsync(request, cancellationToken));
    }

    [HttpPost("templates")]
    [Permission("system:notification-template:update")]
    public async Task<ActionResult<ApiResult<NotificationTemplateResponse>>> CreateTemplateAsync(
        [FromBody] SaveNotificationTemplateRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _notificationService.CreateTemplateAsync(request, cancellationToken));
    }

    [HttpPut("templates/{id:guid}")]
    [Permission("system:notification-template:update")]
    public async Task<ActionResult<ApiResult<NotificationTemplateResponse>>> UpdateTemplateAsync(
        Guid id,
        [FromBody] SaveNotificationTemplateRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _notificationService.UpdateTemplateAsync(id, request, cancellationToken));
    }

    [HttpDelete("templates/{id:guid}")]
    [Permission("system:notification-template:update")]
    public async Task<ActionResult<ApiResult>> DeleteTemplateAsync(Guid id, CancellationToken cancellationToken)
    {
        await _notificationService.DeleteTemplateAsync(id, cancellationToken);
        return Success();
    }
}
