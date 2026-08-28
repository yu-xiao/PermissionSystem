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

    public AiDocumentDraftController(IAiDocumentDraftService draftService)
    {
        _draftService = draftService;
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
}
