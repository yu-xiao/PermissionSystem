using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/workflow/business-bindings")]
public sealed class WorkflowBusinessBindingController : ApiControllerBase
{
    private readonly IWorkflowBusinessBindingService _workflowBusinessBindingService;

    public WorkflowBusinessBindingController(IWorkflowBusinessBindingService workflowBusinessBindingService)
    {
        _workflowBusinessBindingService = workflowBusinessBindingService;
    }

    [HttpGet]
    [Permission("workflow:business-binding:view")]
    public async Task<ActionResult<ApiResult<PagedResult<WorkflowBusinessBindingResponse>>>> GetPagedAsync(
        [FromQuery] WorkflowBusinessBindingQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowBusinessBindingService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("by-business-type/{businessType}")]
    [Permission("workflow:business-binding:view|workflow:instance:start")]
    public async Task<ActionResult<ApiResult<WorkflowBusinessBindingResponse>>> GetByBusinessTypeAsync(
        string businessType,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowBusinessBindingService.GetEnabledByBusinessTypeAsync(businessType, cancellationToken));
    }

    [HttpPost]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:business-binding:create")]
    public async Task<ActionResult<ApiResult<WorkflowBusinessBindingResponse>>> CreateAsync(
        [FromBody] CreateWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowBusinessBindingService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("workflow:business-binding:update")]
    public async Task<ActionResult<ApiResult<WorkflowBusinessBindingResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowBusinessBindingService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("workflow:business-binding:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _workflowBusinessBindingService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/enable")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:business-binding:enable")]
    public async Task<ActionResult<ApiResult<WorkflowBusinessBindingResponse>>> EnableAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowBusinessBindingService.EnableAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/disable")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:business-binding:disable")]
    public async Task<ActionResult<ApiResult<WorkflowBusinessBindingResponse>>> DisableAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowBusinessBindingService.DisableAsync(id, cancellationToken));
    }
}
