using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/workflow/definitions")]
public sealed class WorkflowDefinitionController : ApiControllerBase
{
    private readonly IWorkflowDefinitionService _workflowDefinitionService;

    public WorkflowDefinitionController(IWorkflowDefinitionService workflowDefinitionService)
    {
        _workflowDefinitionService = workflowDefinitionService;
    }

    [HttpGet]
    [Permission("workflow:definition:view")]
    public async Task<ActionResult<ApiResult<PagedResult<WorkflowDefinitionListResponse>>>> GetPagedAsync(
        [FromQuery] WorkflowDefinitionQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowDefinitionService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("workflow:definition:view")]
    public async Task<ActionResult<ApiResult<WorkflowDefinitionDetailResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowDefinitionService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:definition:create")]
    public async Task<ActionResult<ApiResult<WorkflowDefinitionListResponse>>> CreateAsync(
        [FromBody] CreateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowDefinitionService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("workflow:definition:update")]
    public async Task<ActionResult<ApiResult<WorkflowDefinitionListResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowDefinitionService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("workflow:definition:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _workflowDefinitionService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpGet("{id:guid}/designer")]
    [Permission("workflow:definition:view|workflow:definition:design")]
    public async Task<ActionResult<ApiResult<WorkflowDesignerResponse>>> GetDesignerAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowDefinitionService.GetDesignerAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}/designer")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:definition:design")]
    public async Task<ActionResult<ApiResult<WorkflowDesignerResponse>>> SaveDesignerAsync(
        Guid id,
        [FromBody] SaveWorkflowDesignerRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowDefinitionService.SaveDesignerAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/publish")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:definition:publish")]
    public async Task<ActionResult<ApiResult<WorkflowDefinitionListResponse>>> PublishAsync(
        Guid id,
        [FromBody] PublishWorkflowDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowDefinitionService.PublishAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/disable")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:definition:disable")]
    public async Task<ActionResult<ApiResult<WorkflowDefinitionListResponse>>> DisableAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowDefinitionService.DisableAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/copy")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:definition:create")]
    public async Task<ActionResult<ApiResult<WorkflowDefinitionDetailResponse>>> CopyAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowDefinitionService.CopyAsync(id, cancellationToken));
    }
}
