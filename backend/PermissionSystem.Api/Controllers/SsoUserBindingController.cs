using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Sso;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/sso/user-bindings")]
public sealed class SsoUserBindingController : ApiControllerBase
{
    private readonly ISsoManagementService _ssoManagementService;

    public SsoUserBindingController(ISsoManagementService ssoManagementService)
    {
        _ssoManagementService = ssoManagementService;
    }

    [HttpGet]
    [Permission("sso:user-binding:view")]
    public async Task<ActionResult<ApiResult<PagedResult<SsoUserBindingResponse>>>> GetPagedAsync(
        [FromQuery] SsoUserBindingQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoManagementService.GetUserBindingsAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("sso:user-binding:view")]
    public async Task<ActionResult<ApiResult<SsoUserBindingDetailResponse>>> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoManagementService.GetUserBindingAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/unbind")]
    [Permission("sso:user-binding:unbind")]
    public async Task<ActionResult<ApiResult>> UnbindAsync(Guid id, CancellationToken cancellationToken)
    {
        await _ssoManagementService.DeleteUserBindingAsync(id, cancellationToken);
        return Success();
    }

    [HttpDelete("{id:guid}")]
    [Permission("sso:user-binding:unbind")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _ssoManagementService.DeleteUserBindingAsync(id, cancellationToken);
        return Success();
    }
}
