using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/permissions")]
public sealed class PermissionController : ApiControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    [HttpGet]
    [Permission("system:permission:view")]
    public async Task<ActionResult<ApiResult<PagedResult<PermissionResponse>>>> GetPagedAsync(
        [FromQuery] PermissionQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _permissionService.GetPagedAsync(request, cancellationToken));
    }

    [HttpPost]
    [Permission("system:permission:create")]
    public async Task<ActionResult<ApiResult<PermissionResponse>>> CreateAsync(
        [FromBody] CreatePermissionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _permissionService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:permission:update")]
    public async Task<ActionResult<ApiResult<PermissionResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdatePermissionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _permissionService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:permission:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _permissionService.DeleteAsync(id, cancellationToken);
        return Success();
    }
}
