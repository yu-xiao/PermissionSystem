using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.SystemConfigs;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/system-configs")]
public sealed class SystemConfigController : ApiControllerBase
{
    private readonly ISystemConfigService _systemConfigService;

    public SystemConfigController(ISystemConfigService systemConfigService)
    {
        _systemConfigService = systemConfigService;
    }

    [HttpGet]
    [Permission("system:config:view")]
    public async Task<ActionResult<ApiResult<PagedResult<SystemConfigResponse>>>> GetPagedAsync(
        [FromQuery] SystemConfigQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _systemConfigService.GetPagedAsync(request, cancellationToken));
    }

    [HttpPost]
    [Permission("system:config:create")]
    public async Task<ActionResult<ApiResult<SystemConfigResponse>>> CreateAsync(
        [FromBody] CreateSystemConfigRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _systemConfigService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:config:update")]
    public async Task<ActionResult<ApiResult<SystemConfigResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateSystemConfigRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _systemConfigService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:config:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _systemConfigService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpGet("values/{configKey}")]
    [Permission("system:config:view")]
    public async Task<ActionResult<ApiResult<SystemConfigValueResponse>>> GetValueByKeyAsync(
        string configKey,
        [FromQuery] bool revealSensitive,
        CancellationToken cancellationToken)
    {
        return Success(await _systemConfigService.GetValueByKeyAsync(configKey, revealSensitive, cancellationToken));
    }

    [HttpGet("groups/{groupCode}")]
    [Permission("system:config:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<SystemConfigResponse>>>> GetByGroupCodeAsync(
        string groupCode,
        CancellationToken cancellationToken)
    {
        return Success(await _systemConfigService.GetEnabledByGroupCodeAsync(groupCode, cancellationToken));
    }
}
