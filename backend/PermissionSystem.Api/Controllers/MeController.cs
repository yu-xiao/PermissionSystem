using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Menus;
using PermissionSystem.Application.Users;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/me")]
public sealed class MeController : ApiControllerBase
{
    private readonly ICurrentUserAppService _currentUserAppService;

    public MeController(ICurrentUserAppService currentUserAppService)
    {
        _currentUserAppService = currentUserAppService;
    }

    [HttpGet]
    [Permission("system:user:view")]
    public async Task<ActionResult<ApiResult<CurrentUserResponse>>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        return Success(await _currentUserAppService.GetCurrentUserAsync(cancellationToken));
    }

    [HttpGet("menus")]
    [Permission("system:menu:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<MenuTreeResponse>>>> GetCurrentUserMenusAsync(CancellationToken cancellationToken)
    {
        return Success(await _currentUserAppService.GetCurrentUserMenusAsync(cancellationToken));
    }

    [HttpGet("permissions")]
    [Permission("system:user:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyCollection<string>>>> GetCurrentUserPermissionCodesAsync(CancellationToken cancellationToken)
    {
        return Success(await _currentUserAppService.GetCurrentUserPermissionCodesAsync(cancellationToken));
    }
}
