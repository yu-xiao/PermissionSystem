using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai/document-drafts")]
public sealed class AiDocumentDraftController : ApiControllerBase
{
    private readonly IAiDocumentDraftService _draftService;
    private readonly IAiDocumentExecutionService _executionService;

    public AiDocumentDraftController(
        IAiDocumentDraftService draftService,
        IAiDocumentExecutionService executionService)
    {
        _draftService = draftService;
        _executionService = executionService;
    }

    [HttpGet("~/api/ai/business-actions/DemoBusinessOrder/schema")]
    [Permission(AiCenterConstants.DocumentDraftPermission)]
    public async Task<ActionResult<ApiResult<AiBusinessActionSchemaResponse>>> GetSchemaAsync(
        CancellationToken cancellationToken)
    {
        return Success(await _draftService.GetDemoBusinessOrderSchemaAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission(AiCenterConstants.DocumentDraftPermission)]
    public async Task<ActionResult<ApiResult<AiDocumentDraftResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _draftService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission(AiCenterConstants.DocumentDraftPermission)]
    public async Task<ActionResult<ApiResult<AiDocumentDraftResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateAiDocumentDraftRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _draftService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.DocumentDraftPermission)]
    public async Task<ActionResult<ApiResult<AiDocumentDraftResponse>>> CancelAsync(
        Guid id,
        [FromBody] CancelAiDocumentDraftRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _draftService.CancelAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/confirmation")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission(AiCenterConstants.DocumentExecutePermission)]
    [Permission("demo-business-order:create")]
    public async Task<ActionResult<ApiResult<AiDocumentConfirmationResponse>>> ConfirmAsync(
        Guid id,
        [FromBody] CreateAiDocumentConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _executionService.ConfirmAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/execute")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission(AiCenterConstants.DocumentExecutePermission)]
    [Permission("demo-business-order:create")]
    public async Task<ActionResult<ApiResult<AiDocumentExecutionResponse>>> ExecuteAsync(
        Guid id,
        [FromBody] ExecuteAiDocumentDraftRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _executionService.ExecuteAsync(id, request, cancellationToken));
    }
}
