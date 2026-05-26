using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/workflow/tasks")]
public sealed class WorkflowTaskController : ApiControllerBase
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowTaskService _workflowTaskService;

    public WorkflowTaskController(
        IWorkflowEngine workflowEngine,
        IWorkflowTaskService workflowTaskService)
    {
        _workflowEngine = workflowEngine;
        _workflowTaskService = workflowTaskService;
    }

    [HttpGet("todo")]
    [Permission("workflow:task:todo")]
    public async Task<ActionResult<ApiResult<PagedResult<WorkflowTaskResponse>>>> GetTodoAsync(
        [FromQuery] WorkflowTaskQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowTaskService.GetTodoAsync(request, cancellationToken));
    }

    [HttpGet("done")]
    [Permission("workflow:task:todo")]
    public async Task<ActionResult<ApiResult<PagedResult<WorkflowTaskResponse>>>> GetDoneAsync(
        [FromQuery] WorkflowTaskQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowTaskService.GetDoneAsync(request, cancellationToken));
    }

    [HttpPost("{taskId:guid}/approve")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:task:approve")]
    public async Task<ActionResult<ApiResult>> ApproveAsync(
        Guid taskId,
        [FromBody] WorkflowTaskActionRequest request,
        CancellationToken cancellationToken)
    {
        await _workflowEngine.ApproveAsync(taskId, request, cancellationToken);
        return Success();
    }

    [HttpPost("{taskId:guid}/reject")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:task:reject")]
    public async Task<ActionResult<ApiResult>> RejectAsync(
        Guid taskId,
        [FromBody] WorkflowTaskActionRequest request,
        CancellationToken cancellationToken)
    {
        await _workflowEngine.RejectAsync(taskId, request, cancellationToken);
        return Success();
    }

    [HttpPost("{taskId:guid}/transfer")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:task:transfer")]
    public async Task<ActionResult<ApiResult>> TransferAsync(
        Guid taskId,
        [FromBody] TransferWorkflowTaskRequest request,
        CancellationToken cancellationToken)
    {
        await _workflowEngine.TransferAsync(taskId, request, cancellationToken);
        return Success();
    }

    [HttpPost("{taskId:guid}/add-sign")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:task:add-sign")]
    public async Task<ActionResult<ApiResult>> AddSignAsync(
        Guid taskId,
        [FromBody] AddSignWorkflowTaskRequest request,
        CancellationToken cancellationToken)
    {
        await _workflowEngine.AddSignAsync(taskId, request, cancellationToken);
        return Success();
    }
}
