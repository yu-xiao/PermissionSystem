using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Sso;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/sso/providers/{providerId:guid}/department-mappings")]
public sealed class SsoDepartmentMappingController : ApiControllerBase
{
    private readonly ISsoManagementService _ssoManagementService;

    public SsoDepartmentMappingController(ISsoManagementService ssoManagementService)
    {
        _ssoManagementService = ssoManagementService;
    }

    [HttpGet]
    [Permission("sso:department-mapping:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<SsoDepartmentMappingResponse>>>> GetListAsync(
        Guid providerId,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoManagementService.GetDepartmentMappingsAsync(providerId, cancellationToken));
    }

    [HttpPut]
    [Permission("sso:department-mapping:update")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<SsoDepartmentMappingResponse>>>> SaveAsync(
        Guid providerId,
        [FromBody] IReadOnlyCollection<SsoDepartmentMappingRequest>? request,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoManagementService.SaveDepartmentMappingsAsync(providerId, request ?? [], cancellationToken));
    }
}
