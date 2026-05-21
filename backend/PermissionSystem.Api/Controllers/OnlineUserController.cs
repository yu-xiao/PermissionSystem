using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/online-users")]
public sealed class OnlineUserController : ApiControllerBase
{
    private readonly IUserSessionService _userSessionService;

    public OnlineUserController(IUserSessionService userSessionService)
    {
        _userSessionService = userSessionService;
    }

    [HttpGet]
    [Permission("system:online-user:view")]
    public async Task<ActionResult<ApiResult<PagedResult<OnlineUserResponse>>>> GetPagedAsync(
        [FromQuery] OnlineUserQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _userSessionService.GetOnlineUsersAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("system:online-user:view")]
    public async Task<ActionResult<ApiResult<OnlineUserResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _userSessionService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/kickout")]
    [Permission("system:online-user:kickout")]
    public async Task<ActionResult<ApiResult>> KickoutAsync(
        Guid id,
        [FromBody] KickoutUserSessionRequest request,
        CancellationToken cancellationToken)
    {
        await _userSessionService.KickoutAsync(id, request.Reason, cancellationToken);
        return Success();
    }
}
