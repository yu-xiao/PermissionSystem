using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Sso;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/sso/providers/{providerId:guid}/role-mappings")]
public sealed class SsoRoleMappingController : ApiControllerBase
{
    private readonly ISsoManagementService _ssoManagementService;

    public SsoRoleMappingController(ISsoManagementService ssoManagementService)
    {
        _ssoManagementService = ssoManagementService;
    }

    [HttpGet]
    [Permission("sso:role-mapping:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<SsoRoleMappingResponse>>>> GetListAsync(
        Guid providerId,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoManagementService.GetRoleMappingsAsync(providerId, cancellationToken));
    }

    [HttpPut]
    [Permission("sso:role-mapping:update")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<SsoRoleMappingResponse>>>> SaveAsync(
        Guid providerId,
        [FromBody] IReadOnlyCollection<SsoRoleMappingRequest>? request,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoManagementService.SaveRoleMappingsAsync(providerId, request ?? [], cancellationToken));
    }
}
