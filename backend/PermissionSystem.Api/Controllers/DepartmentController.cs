using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Departments;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/departments")]
public sealed class DepartmentController : ApiControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet("tree")]
    [Permission("system:department:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<DepartmentTreeResponse>>>> GetTreeAsync(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        return Success(await _departmentService.GetTreeAsync(tenantId, cancellationToken));
    }

    [HttpPost]
    [Permission("system:department:create")]
    public async Task<ActionResult<ApiResult<DepartmentTreeResponse>>> CreateAsync(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _departmentService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:department:update")]
    public async Task<ActionResult<ApiResult<DepartmentTreeResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _departmentService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:department:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _departmentService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPatch("{id:guid}/enabled")]
    [Permission("system:department:update")]
    public async Task<ActionResult<ApiResult>> SetEnabledAsync(
        Guid id,
        [FromBody] SetDepartmentEnabledRequest request,
        CancellationToken cancellationToken)
    {
        await _departmentService.SetEnabledAsync(id, request, cancellationToken);
        return Success();
    }
}
