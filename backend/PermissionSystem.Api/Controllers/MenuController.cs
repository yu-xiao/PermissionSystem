using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Menus;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/menus")]
public sealed class MenuController : ApiControllerBase
{
    private readonly IMenuService _menuService;

    public MenuController(IMenuService menuService)
    {
        _menuService = menuService;
    }

    [HttpGet("tree")]
    [Permission("system:menu:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<MenuTreeResponse>>>> GetTreeAsync(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        return Success(await _menuService.GetTreeAsync(tenantId, cancellationToken));
    }

    [HttpPost]
    [Permission("system:menu:create")]
    public async Task<ActionResult<ApiResult<MenuTreeResponse>>> CreateAsync(
        [FromBody] CreateMenuRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _menuService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:menu:update")]
    public async Task<ActionResult<ApiResult<MenuTreeResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateMenuRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _menuService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:menu:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _menuService.DeleteAsync(id, cancellationToken);
        return Success();
    }
}
