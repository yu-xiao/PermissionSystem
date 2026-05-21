using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.OperationLogs;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/operation-logs")]
public sealed class OperationLogController : ApiControllerBase
{
    private readonly IOperationLogService _operationLogService;

    public OperationLogController(IOperationLogService operationLogService)
    {
        _operationLogService = operationLogService;
    }

    [HttpGet]
    [Permission("system:operation-log:view")]
    public async Task<ActionResult<ApiResult<PagedResult<OperationLogResponse>>>> GetPagedAsync(
        [FromQuery] OperationLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _operationLogService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("system:operation-log:view")]
    public async Task<ActionResult<ApiResult<OperationLogDetailResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _operationLogService.GetByIdAsync(id, cancellationToken));
    }
}
