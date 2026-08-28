using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai/providers")]
public sealed class AiProviderController : ApiControllerBase
{
    private readonly IAiProviderService _providerService;

    public AiProviderController(IAiProviderService providerService)
    {
        _providerService = providerService;
    }

    [HttpGet]
    [Permission(AiCenterConstants.ProviderViewPermission)]
    public async Task<ActionResult<ApiResult<PagedResult<AiProviderListResponse>>>> GetPagedAsync(
        [FromQuery] AiProviderQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _providerService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission(AiCenterConstants.ProviderViewPermission)]
    public async Task<ActionResult<ApiResult<AiProviderDetailResponse>>> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _providerService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ProviderCreatePermission)]
    public async Task<ActionResult<ApiResult<AiProviderDetailResponse>>> CreateAsync(
        [FromBody] CreateAiProviderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _providerService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ProviderUpdatePermission)]
    public async Task<ActionResult<ApiResult<AiProviderDetailResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateAiProviderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _providerService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ProviderDeletePermission)]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _providerService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPut("{id:guid}/enabled")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ProviderUpdatePermission)]
    public async Task<ActionResult<ApiResult>> SetEnabledAsync(
        Guid id,
        [FromBody] SetAiProviderEnabledRequest request,
        CancellationToken cancellationToken)
    {
        await _providerService.SetEnabledAsync(id, request, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/default")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ProviderUpdatePermission)]
    public async Task<ActionResult<ApiResult>> SetDefaultAsync(Guid id, CancellationToken cancellationToken)
    {
        await _providerService.SetDefaultAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/test")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ProviderTestPermission)]
    public async Task<ActionResult<ApiResult<AiProviderConnectionTestResult>>> TestAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _providerService.TestAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}/compliance")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.ProviderCompliancePermission)]
    public async Task<ActionResult<ApiResult>> SetComplianceAsync(
        Guid id,
        [FromBody] SetAiProviderComplianceRequest request,
        CancellationToken cancellationToken)
    {
        await _providerService.SetComplianceAsync(id, request, cancellationToken);
        return Success();
    }
}
