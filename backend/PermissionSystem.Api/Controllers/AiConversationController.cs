using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai/conversations")]
public sealed class AiConversationController : ApiControllerBase
{
    private readonly IAiConversationService _conversationService;

    public AiConversationController(IAiConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpGet]
    [Permission(AiCenterConstants.ConversationViewPermission)]
    public async Task<ActionResult<ApiResult<PagedResult<AiConversationListResponse>>>> GetPagedAsync(
        [FromQuery] AiConversationQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _conversationService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission(AiCenterConstants.ConversationViewPermission)]
    public async Task<ActionResult<ApiResult<AiConversationDetailResponse>>> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _conversationService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ChatUsePermission)]
    public async Task<ActionResult<ApiResult<AiConversationDetailResponse>>> CreateAsync(
        [FromBody] CreateAiConversationRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _conversationService.CreateAsync(request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ChatUsePermission)]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _conversationService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/messages")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ChatUsePermission)]
    public async Task<ActionResult<ApiResult<AiRunResponse>>> SendMessageAsync(
        Guid id,
        [FromBody] SendAiMessageRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _conversationService.SendMessageAsync(id, request, cancellationToken));
    }

    [HttpGet("~/api/ai/runs/{runId:guid}")]
    [Permission(AiCenterConstants.ConversationViewPermission)]
    public async Task<ActionResult<ApiResult<AiRunResponse>>> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        return Success(await _conversationService.GetRunAsync(runId, cancellationToken));
    }

    [HttpGet("~/api/ai/runs/{runId:guid}/citations")]
    [Permission(AiCenterConstants.ConversationViewPermission)]
    public async Task<ActionResult<ApiResult<IReadOnlyList<AiToolCitation>>>> GetCitationsAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        return Success(await _conversationService.GetCitationsAsync(runId, cancellationToken));
    }

    [HttpPost("~/api/ai/runs/{runId:guid}/cancel")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ChatUsePermission)]
    public async Task<ActionResult<ApiResult>> CancelRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        await _conversationService.CancelRunAsync(runId, cancellationToken);
        return Success();
    }
}
