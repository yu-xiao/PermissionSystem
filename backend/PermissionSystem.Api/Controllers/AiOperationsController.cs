using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai")]
public sealed class AiOperationsController : ApiControllerBase
{
    private readonly IAiOperationsService _operationsService;

    public AiOperationsController(IAiOperationsService operationsService)
    {
        _operationsService = operationsService;
    }

    [HttpGet("runs/{runId:guid}/feedback")]
    [Permission(AiCenterConstants.ChatUsePermission)]
    public async Task<ActionResult<ApiResult<AiFeedbackResponse?>>> GetFeedbackAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        return Success(await _operationsService.GetMyFeedbackAsync(runId, cancellationToken));
    }

    [HttpPut("runs/{runId:guid}/feedback")]
    [Permission(AiCenterConstants.ChatUsePermission)]
    public async Task<ActionResult<ApiResult<AiFeedbackResponse>>> SaveFeedbackAsync(
        Guid runId,
        [FromBody] SaveAiFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _operationsService.SaveMyFeedbackAsync(runId, request, cancellationToken));
    }

    [HttpGet("operations/summary")]
    [Permission(AiCenterConstants.OperationsViewPermission)]
    public async Task<ActionResult<ApiResult<AiOperationsSummaryResponse>>> GetSummaryAsync(
        [FromQuery] AiOperationsQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _operationsService.GetSummaryAsync(request, cancellationToken));
    }
}
