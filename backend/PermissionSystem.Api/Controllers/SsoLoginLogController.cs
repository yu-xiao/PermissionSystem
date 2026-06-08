using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Sso;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/sso/login-logs")]
public sealed class SsoLoginLogController : ApiControllerBase
{
    private readonly ISsoManagementService _ssoManagementService;

    public SsoLoginLogController(ISsoManagementService ssoManagementService)
    {
        _ssoManagementService = ssoManagementService;
    }

    [HttpGet]
    [Permission("sso:login-log:view")]
    public async Task<ActionResult<ApiResult<PagedResult<SsoLoginLogResponse>>>> GetPagedAsync(
        [FromQuery] SsoLoginLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoManagementService.GetLoginLogsAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("sso:login-log:view")]
    public async Task<ActionResult<ApiResult<SsoLoginLogResponse>>> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoManagementService.GetLoginLogAsync(id, cancellationToken));
    }
}
