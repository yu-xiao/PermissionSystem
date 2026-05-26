using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/workflow/cc")]
public sealed class WorkflowCcController : ApiControllerBase
{
    private readonly IWorkflowTaskService _workflowTaskService;

    public WorkflowCcController(IWorkflowTaskService workflowTaskService)
    {
        _workflowTaskService = workflowTaskService;
    }

    [HttpGet("my")]
    [Permission("workflow:cc:view")]
    public async Task<ActionResult<ApiResult<PagedResult<WorkflowCcResponse>>>> GetMyCcAsync(
        [FromQuery] WorkflowCcQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _workflowTaskService.GetMyCcAsync(request, cancellationToken));
    }

    [HttpPost("{ccId:guid}/read")]
    [Permission("workflow:cc:view")]
    public async Task<ActionResult<ApiResult>> MarkAsReadAsync(
        Guid ccId,
        CancellationToken cancellationToken)
    {
        await _workflowTaskService.MarkCcAsReadAsync(ccId, cancellationToken);
        return Success();
    }
}
