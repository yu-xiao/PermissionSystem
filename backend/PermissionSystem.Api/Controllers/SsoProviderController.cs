using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Sso;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/sso/providers")]
public sealed class SsoProviderController : ApiControllerBase
{
    private readonly ISsoProviderService _ssoProviderService;

    public SsoProviderController(ISsoProviderService ssoProviderService)
    {
        _ssoProviderService = ssoProviderService;
    }

    [HttpGet]
    [Permission("sso:provider:view")]
    public async Task<ActionResult<ApiResult<PagedResult<SsoProviderListResponse>>>> GetPagedAsync(
        [FromQuery] SsoProviderQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoProviderService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("enabled")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult<IReadOnlyList<SsoProviderListResponse>>>> GetEnabledAsync(
        CancellationToken cancellationToken)
    {
        return Success(await _ssoProviderService.GetEnabledAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("sso:provider:view")]
    public async Task<ActionResult<ApiResult<SsoProviderDetailResponse>>> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoProviderService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission("sso:provider:create")]
    public async Task<ActionResult<ApiResult<SsoProviderDetailResponse>>> CreateAsync(
        [FromBody] CreateSsoProviderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoProviderService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("sso:provider:update")]
    public async Task<ActionResult<ApiResult<SsoProviderDetailResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateSsoProviderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoProviderService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("sso:provider:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _ssoProviderService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/enable")]
    [Permission("sso:provider:enable")]
    public async Task<ActionResult<ApiResult>> EnableAsync(Guid id, CancellationToken cancellationToken)
    {
        await _ssoProviderService.SetEnabledAsync(id, true, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/disable")]
    [Permission("sso:provider:disable")]
    public async Task<ActionResult<ApiResult>> DisableAsync(Guid id, CancellationToken cancellationToken)
    {
        await _ssoProviderService.SetEnabledAsync(id, false, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/test")]
    [Permission("sso:provider:test")]
    public async Task<ActionResult<ApiResult<SsoProviderTestResponse>>> TestAsync(
        Guid id,
        [FromBody] TestSsoProviderRequest? request,
        CancellationToken cancellationToken)
    {
        return Success(await _ssoProviderService.TestAsync(id, request ?? new TestSsoProviderRequest(), cancellationToken));
    }
}
