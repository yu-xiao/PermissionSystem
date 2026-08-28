using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai/governance")]
public sealed class AiGovernanceController : ApiControllerBase
{
    private readonly IAiModelRouteService _routeService;
    private readonly IAiBudgetService _budgetService;

    public AiGovernanceController(IAiModelRouteService routeService, IAiBudgetService budgetService)
    {
        _routeService = routeService;
        _budgetService = budgetService;
    }

    [HttpGet("routes")]
    [Permission(AiCenterConstants.GovernanceViewPermission)]
    public async Task<ActionResult<ApiResult<IReadOnlyList<AiModelRoutePolicyResponse>>>> GetRoutesAsync(
        CancellationToken cancellationToken)
    {
        return Success(await _routeService.GetPoliciesAsync(cancellationToken));
    }

    [HttpPut("routes")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult<AiModelRoutePolicyResponse>>> SaveRouteAsync(
        [FromBody] SaveAiModelRoutePolicyRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _routeService.SavePolicyAsync(request, cancellationToken));
    }

    [HttpGet("providers")]
    [Permission(AiCenterConstants.GovernanceViewPermission)]
    public async Task<ActionResult<ApiResult<IReadOnlyList<AiModelRouteProviderOptionResponse>>>> GetProvidersAsync(
        CancellationToken cancellationToken)
    {
        return Success(await _routeService.GetProviderOptionsAsync(cancellationToken));
    }

    [HttpGet("budgets")]
    [Permission(AiCenterConstants.GovernanceViewPermission)]
    public async Task<ActionResult<ApiResult<IReadOnlyList<AiBudgetPolicyResponse>>>> GetBudgetsAsync(
        CancellationToken cancellationToken)
    {
        return Success(await _budgetService.GetPoliciesAsync(cancellationToken));
    }

    [HttpPut("budgets")]
    [IdempotencyKey]
    [Permission(AiCenterConstants.GovernanceManagePermission)]
    public async Task<ActionResult<ApiResult<AiBudgetPolicyResponse>>> SaveBudgetAsync(
        [FromBody] SaveAiBudgetPolicyRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _budgetService.SavePolicyAsync(request, cancellationToken));
    }
}
