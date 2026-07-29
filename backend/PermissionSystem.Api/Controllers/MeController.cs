using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Menus;
using PermissionSystem.Application.Users;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/me")]
[Authorize]
public sealed class MeController : ApiControllerBase
{
    private readonly ICurrentUserAppService _currentUserAppService;
    private readonly IMeService _meService;
    private readonly ITraceContextAccessor _traceContextAccessor;
    private readonly IClientIpAccessor _clientIpAccessor;

    public MeController(
        ICurrentUserAppService currentUserAppService,
        IMeService meService,
        ITraceContextAccessor traceContextAccessor,
        IClientIpAccessor clientIpAccessor)
    {
        _currentUserAppService = currentUserAppService;
        _meService = meService;
        _traceContextAccessor = traceContextAccessor;
        _clientIpAccessor = clientIpAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<CurrentUserResponse>>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        return Success(await _currentUserAppService.GetCurrentUserAsync(cancellationToken));
    }

    [HttpGet("menus")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<MenuTreeResponse>>>> GetCurrentUserMenusAsync(CancellationToken cancellationToken)
    {
        return Success(await _currentUserAppService.GetCurrentUserMenusAsync(cancellationToken));
    }

    [HttpGet("permissions")]
    public async Task<ActionResult<ApiResult<IReadOnlyCollection<string>>>> GetCurrentUserPermissionCodesAsync(CancellationToken cancellationToken)
    {
        return Success(await _currentUserAppService.GetCurrentUserPermissionCodesAsync(cancellationToken));
    }

    [HttpGet("profile")]
    public async Task<ActionResult<ApiResult<MyProfileResponse>>> GetProfileAsync(CancellationToken cancellationToken)
    {
        return Success(await _meService.GetProfileAsync(cancellationToken));
    }

    [HttpPut("profile")]
    [PreventDuplicateSubmit]
    public async Task<ActionResult<ApiResult<MyProfileResponse>>> UpdateProfileAsync(
        [FromBody] UpdateMyProfileRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _meService.UpdateProfileAsync(request, cancellationToken));
    }

    [HttpPut("password")]
    [PreventDuplicateSubmit]
    public async Task<ActionResult<ApiResult>> ChangePasswordAsync(
        [FromBody] ChangeMyPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _meService.ChangePasswordAsync(request, cancellationToken);
        return Success("密码修改成功，请重新登录。");
    }

    [HttpPost("logout")]
    [PreventDuplicateSubmit]
    public async Task<ActionResult<ApiResult>> LogoutAsync(
        [FromBody] LogoutMySessionRequest? request,
        CancellationToken cancellationToken)
    {
        await _meService.LogoutAsync(WithRequestContext(request), cancellationToken);
        return Success("退出登录成功。");
    }

    [HttpPost("logout-all")]
    [PreventDuplicateSubmit]
    public async Task<ActionResult<ApiResult>> LogoutAllAsync(CancellationToken cancellationToken)
    {
        await _meService.LogoutAllAsync(WithRequestContext(null), cancellationToken);
        return Success("已退出所有设备。");
    }

    private LogoutMySessionRequest WithRequestContext(LogoutMySessionRequest? request)
    {
        return new LogoutMySessionRequest
        {
            RefreshToken = request?.RefreshToken,
            IpAddress = _clientIpAccessor.GetClientIp(HttpContext),
            UserAgent = Request.Headers.UserAgent.ToString(),
            TraceId = !string.IsNullOrWhiteSpace(_traceContextAccessor.TraceId)
                ? _traceContextAccessor.TraceId
                : Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier
        };
    }

}
