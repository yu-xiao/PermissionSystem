using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/inbox-messages")]
public sealed class InboxMessageController : ApiControllerBase
{
    private readonly IInboxService _inboxService;

    public InboxMessageController(IInboxService inboxService)
    {
        _inboxService = inboxService;
    }

    [HttpGet]
    [Permission("system:inbox:view")]
    public async Task<ActionResult<ApiResult<PagedResult<InboxMessageResponse>>>> GetPagedAsync(
        [FromQuery] InboxMessageQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _inboxService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("system:inbox:view")]
    public async Task<ActionResult<ApiResult<InboxMessageDetailResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _inboxService.GetByIdAsync(id, cancellationToken));
    }
}
