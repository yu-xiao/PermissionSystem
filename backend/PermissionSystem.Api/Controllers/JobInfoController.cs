using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Jobs;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/jobs")]
public sealed class JobInfoController : ApiControllerBase
{
    private readonly IJobInfoService _jobInfoService;

    public JobInfoController(IJobInfoService jobInfoService)
    {
        _jobInfoService = jobInfoService;
    }

    [HttpGet]
    [Permission("system:job:view")]
    public async Task<ActionResult<ApiResult<PagedResult<JobInfoResponse>>>> GetPagedAsync(
        [FromQuery] JobInfoQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _jobInfoService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("logs")]
    [Permission("system:job:view")]
    public async Task<ActionResult<ApiResult<PagedResult<JobExecutionLogResponse>>>> GetLogsAsync(
        [FromQuery] JobExecutionLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _jobInfoService.GetLogsAsync(request, cancellationToken));
    }

    [HttpPost("{jobName}/trigger")]
    [Permission("system:job:trigger")]
    public async Task<ActionResult<ApiResult>> TriggerAsync(
        string jobName,
        CancellationToken cancellationToken)
    {
        await _jobInfoService.TriggerAsync(Uri.UnescapeDataString(jobName), cancellationToken);
        return Success();
    }

    [HttpPost("{jobName}/enable")]
    [Permission("system:job:trigger")]
    public async Task<ActionResult<ApiResult>> EnableAsync(
        string jobName,
        CancellationToken cancellationToken)
    {
        await _jobInfoService.EnableAsync(Uri.UnescapeDataString(jobName), cancellationToken);
        return Success();
    }

    [HttpPost("{jobName}/disable")]
    [Permission("system:job:trigger")]
    public async Task<ActionResult<ApiResult>> DisableAsync(
        string jobName,
        CancellationToken cancellationToken)
    {
        await _jobInfoService.DisableAsync(Uri.UnescapeDataString(jobName), cancellationToken);
        return Success();
    }
}
