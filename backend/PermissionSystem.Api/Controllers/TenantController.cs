using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/tenants")]
public sealed class TenantController : ApiControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet]
    [Permission("system:tenant:view")]
    public async Task<ActionResult<ApiResult<PagedResult<TenantResponse>>>> GetPagedAsync(
        [FromQuery] TenantQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _tenantService.GetPagedAsync(request, cancellationToken));
    }

    [HttpPost]
    [Permission("system:tenant:create")]
    public async Task<ActionResult<ApiResult<TenantResponse>>> CreateAsync(
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _tenantService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:tenant:update")]
    public async Task<ActionResult<ApiResult<TenantResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _tenantService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:guid}/enabled")]
    [Permission("system:tenant:disable")]
    public async Task<ActionResult<ApiResult>> SetEnabledAsync(
        Guid id,
        [FromBody] SetTenantEnabledRequest request,
        CancellationToken cancellationToken)
    {
        await _tenantService.SetEnabledAsync(id, request, cancellationToken);
        return Success();
    }
}
