using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Roles;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/roles")]
public sealed class RoleController : ApiControllerBase
{
    private readonly IRoleService _roleService;

    public RoleController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    [Permission("system:role:view")]
    public async Task<ActionResult<ApiResult<PagedResult<RoleResponse>>>> GetPagedAsync(
        [FromQuery] RoleQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _roleService.GetPagedAsync(request, cancellationToken));
    }

    [HttpPost]
    [Permission("system:role:create")]
    public async Task<ActionResult<ApiResult<RoleResponse>>> CreateAsync(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _roleService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:role:update")]
    public async Task<ActionResult<ApiResult<RoleResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _roleService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:role:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _roleService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/menus")]
    [Permission("system:role:update")]
    public async Task<ActionResult<ApiResult>> AssignMenusAsync(
        Guid id,
        [FromBody] AssignRoleMenusRequest request,
        CancellationToken cancellationToken)
    {
        await _roleService.AssignMenusAsync(id, request, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/permissions")]
    [Permission("system:role:update")]
    public async Task<ActionResult<ApiResult>> AssignPermissionsAsync(
        Guid id,
        [FromBody] AssignRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        await _roleService.AssignPermissionsAsync(id, request, cancellationToken);
        return Success();
    }
}
