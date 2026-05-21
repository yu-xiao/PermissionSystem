using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/outbox-messages")]
public sealed class OutboxMessageController : ApiControllerBase
{
    private readonly IOutboxService _outboxService;

    public OutboxMessageController(IOutboxService outboxService)
    {
        _outboxService = outboxService;
    }

    [HttpGet]
    [Permission("system:outbox:view")]
    public async Task<ActionResult<ApiResult<PagedResult<OutboxMessageResponse>>>> GetPagedAsync(
        [FromQuery] OutboxMessageQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _outboxService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("system:outbox:view")]
    public async Task<ActionResult<ApiResult<OutboxMessageDetailResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _outboxService.GetByIdAsync(id, cancellationToken));
    }
}
