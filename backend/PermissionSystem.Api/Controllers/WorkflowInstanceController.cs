using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/workflow/instances")]
public sealed class WorkflowInstanceController : ApiControllerBase
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowTaskService _workflowTaskService;

    public WorkflowInstanceController(
        IWorkflowEngine workflowEngine,
        IWorkflowTaskService workflowTaskService)
    {
        _workflowEngine = workflowEngine;
        _workflowTaskService = workflowTaskService;
    }

    [HttpPost("start")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:instance:start")]
    public async Task<ActionResult<ApiResult<WorkflowInstanceDetailResponse>>> StartAsync(
        [FromBody] StartWorkflowInstanceRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowEngine.StartAsync(request, cancellationToken));
    }

    [HttpGet("my-started")]
    [Permission("workflow:instance:view")]
    public async Task<ActionResult<ApiResult<PagedResult<WorkflowInstanceResponse>>>> GetMyStartedAsync(
        [FromQuery] WorkflowInstanceQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowTaskService.GetMyStartedAsync(request, cancellationToken));
    }

    [HttpGet("{instanceId:guid}")]
    [Permission("workflow:instance:view")]
    public async Task<ActionResult<ApiResult<WorkflowInstanceDetailResponse>>> GetDetailAsync(
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowTaskService.GetInstanceDetailAsync(instanceId, cancellationToken));
    }

    [HttpGet("{instanceId:guid}/records")]
    [Permission("workflow:instance:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyCollection<WorkflowRecordResponse>>>> GetRecordsAsync(
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowTaskService.GetRecordsAsync(instanceId, cancellationToken));
    }

    [HttpPost("{instanceId:guid}/withdraw")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("workflow:instance:withdraw")]
    public async Task<ActionResult<ApiResult>> WithdrawAsync(
        Guid instanceId,
        [FromBody] WorkflowTaskActionRequest request,
        CancellationToken cancellationToken)
    {
        await _workflowEngine.WithdrawAsync(instanceId, request, cancellationToken);
        return Success();
    }
}
