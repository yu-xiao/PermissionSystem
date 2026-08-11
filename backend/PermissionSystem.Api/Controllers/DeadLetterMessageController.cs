using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/dead-letter-messages")]
public sealed class DeadLetterMessageController : ApiControllerBase
{
    private readonly IDeadLetterMessageService _deadLetterMessageService;

    public DeadLetterMessageController(IDeadLetterMessageService deadLetterMessageService)
    {
        _deadLetterMessageService = deadLetterMessageService;
    }

    [HttpGet]
    [Permission("system:dead-letter:view")]
    public async Task<ActionResult<ApiResult<PagedResult<DeadLetterMessageResponse>>>> GetPagedAsync(
        [FromQuery] DeadLetterMessageQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _deadLetterMessageService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("system:dead-letter:view")]
    public async Task<ActionResult<ApiResult<DeadLetterMessageDetailResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _deadLetterMessageService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/replay")]
    [Permission("system:dead-letter:replay")]
    public async Task<ActionResult<ApiResult>> ReplayAsync(Guid id, CancellationToken cancellationToken)
    {
        await _deadLetterMessageService.ReplayAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/discard")]
    [Permission("system:dead-letter:discard")]
    public async Task<ActionResult<ApiResult>> DiscardAsync(
        Guid id,
        [FromBody] DiscardDeadLetterMessageRequest request,
        CancellationToken cancellationToken)
    {
        await _deadLetterMessageService.DiscardAsync(id, request, cancellationToken);
        return Success();
    }
}
