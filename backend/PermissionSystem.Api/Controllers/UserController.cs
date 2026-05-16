using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Users;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/users")]
public sealed class UserController : ApiControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [Permission("system:user:view")]
    public async Task<ActionResult<ApiResult<PagedResult<UserResponse>>>> GetPagedAsync(
        [FromQuery] UserQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _userService.GetPagedAsync(request, cancellationToken));
    }

    [HttpPost]
    [Permission("system:user:create")]
    public async Task<ActionResult<ApiResult<UserResponse>>> CreateAsync(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _userService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:user:update")]
    public async Task<ActionResult<ApiResult<UserResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _userService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:user:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPatch("{id:guid}/enabled")]
    [Permission("system:user:update")]
    public async Task<ActionResult<ApiResult>> SetEnabledAsync(
        Guid id,
        [FromBody] SetUserEnabledRequest request,
        CancellationToken cancellationToken)
    {
        await _userService.SetEnabledAsync(id, request, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/reset-password")]
    [Permission("system:user:update")]
    public async Task<ActionResult<ApiResult>> ResetPasswordAsync(
        Guid id,
        [FromBody] ResetUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _userService.ResetPasswordAsync(id, request, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/roles")]
    [Permission("system:user:update")]
    public async Task<ActionResult<ApiResult>> AssignRolesAsync(
        Guid id,
        [FromBody] AssignUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        await _userService.AssignRolesAsync(id, request, cancellationToken);
        return Success();
    }
}
