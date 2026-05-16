using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/scheduled-tasks")]
public sealed class ScheduledTaskController : ApiControllerBase
{
    private readonly IScheduledTaskService _scheduledTaskService;

    public ScheduledTaskController(IScheduledTaskService scheduledTaskService)
    {
        _scheduledTaskService = scheduledTaskService;
    }

    [HttpGet]
    [Permission("system:scheduled-task:view")]
    public async Task<ActionResult<ApiResult<PagedResult<ScheduledTaskResponse>>>> GetPagedAsync(
        [FromQuery] ScheduledTaskQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _scheduledTaskService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}/logs")]
    [Permission("system:scheduled-task:view")]
    public async Task<ActionResult<ApiResult<PagedResult<ScheduledTaskExecutionLogResponse>>>> GetLogsAsync(
        Guid id,
        [FromQuery] ScheduledTaskLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _scheduledTaskService.GetLogsAsync(id, request, cancellationToken));
    }

    [HttpPost]
    [Permission("system:scheduled-task:create")]
    public async Task<ActionResult<ApiResult<ScheduledTaskResponse>>> CreateAsync(
        [FromBody] CreateScheduledTaskRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _scheduledTaskService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:scheduled-task:update")]
    public async Task<ActionResult<ApiResult<ScheduledTaskResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateScheduledTaskRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _scheduledTaskService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:scheduled-task:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _scheduledTaskService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/enable")]
    [Permission("system:scheduled-task:update")]
    public async Task<ActionResult<ApiResult>> EnableAsync(Guid id, CancellationToken cancellationToken)
    {
        await _scheduledTaskService.EnableAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/disable")]
    [Permission("system:scheduled-task:update")]
    public async Task<ActionResult<ApiResult>> DisableAsync(Guid id, CancellationToken cancellationToken)
    {
        await _scheduledTaskService.DisableAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/trigger")]
    [Permission("system:scheduled-task:trigger")]
    public async Task<ActionResult<ApiResult>> TriggerAsync(Guid id, CancellationToken cancellationToken)
    {
        await _scheduledTaskService.TriggerAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("sync")]
    [Permission("system:scheduled-task:update")]
    public async Task<ActionResult<ApiResult>> SyncAsync(CancellationToken cancellationToken)
    {
        await _scheduledTaskService.SyncEnabledTasksAsync(cancellationToken);
        return Success();
    }
}
