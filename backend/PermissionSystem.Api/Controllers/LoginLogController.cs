using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.LoginLogs;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/login-logs")]
public sealed class LoginLogController : ApiControllerBase
{
    private readonly ILoginLogService _loginLogService;

    public LoginLogController(ILoginLogService loginLogService)
    {
        _loginLogService = loginLogService;
    }

    [HttpGet]
    [Permission("system:login-log:view")]
    public async Task<ActionResult<ApiResult<PagedResult<LoginLogResponse>>>> GetPagedAsync(
        [FromQuery] LoginLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _loginLogService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("system:login-log:view")]
    public async Task<ActionResult<ApiResult<LoginLogResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _loginLogService.GetByIdAsync(id, cancellationToken));
    }
}
