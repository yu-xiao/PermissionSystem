using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Security;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/security")]
public sealed class SecurityPolicyController : ApiControllerBase
{
    private readonly ISecurityPolicyService _securityPolicyService;

    public SecurityPolicyController(ISecurityPolicyService securityPolicyService)
    {
        _securityPolicyService = securityPolicyService;
    }

    [HttpGet("policy")]
    [Permission("security:policy:view")]
    public async Task<ActionResult<ApiResult<SecurityPolicyResponse>>> GetPolicyAsync(CancellationToken cancellationToken)
    {
        return Success(await _securityPolicyService.GetPolicyAsync(cancellationToken));
    }

    [HttpPut("policy")]
    [Permission("security:policy:update")]
    public async Task<ActionResult<ApiResult<SecurityPolicyResponse>>> UpdatePolicyAsync(
        [FromBody] UpdateSecurityPolicyRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _securityPolicyService.UpdatePolicyAsync(request, cancellationToken));
    }

    [HttpPost("verification/send")]
    [Permission("security:verification:send")]
    public async Task<ActionResult<ApiResult<SendSensitiveVerificationResponse>>> SendVerificationAsync(
        [FromBody] SendSensitiveVerificationRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _securityPolicyService.SendVerificationAsync(request, cancellationToken));
    }

    [HttpPost("verification/verify")]
    [Permission("security:verification:verify")]
    public async Task<ActionResult<ApiResult>> VerifyAsync(
        [FromBody] VerifySensitiveOperationRequest request,
        CancellationToken cancellationToken)
    {
        await _securityPolicyService.VerifyAsync(request, cancellationToken);
        return Success();
    }

    [HttpGet("ip-rules")]
    [Permission("security:ip-rule:view")]
    public async Task<ActionResult<ApiResult<PagedResult<IpAccessRuleResponse>>>> GetIpRulesAsync(
        [FromQuery] IpAccessRuleQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _securityPolicyService.GetIpRulesAsync(request, cancellationToken));
    }

    [HttpPost("ip-rules")]
    [Permission("security:ip-rule:create")]
    public async Task<ActionResult<ApiResult<IpAccessRuleResponse>>> CreateIpRuleAsync(
        [FromBody] CreateIpAccessRuleRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _securityPolicyService.CreateIpRuleAsync(request, cancellationToken));
    }

    [HttpPut("ip-rules/{id:guid}")]
    [Permission("security:ip-rule:update")]
    public async Task<ActionResult<ApiResult<IpAccessRuleResponse>>> UpdateIpRuleAsync(
        Guid id,
        [FromBody] UpdateIpAccessRuleRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _securityPolicyService.UpdateIpRuleAsync(id, request, cancellationToken));
    }

    [HttpDelete("ip-rules/{id:guid}")]
    [Permission("security:ip-rule:delete")]
    public async Task<ActionResult<ApiResult>> DeleteIpRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        await _securityPolicyService.DeleteIpRuleAsync(id, cancellationToken);
        return Success();
    }

    [HttpGet("login-failures")]
    [Permission("security:login-failure:view")]
    public async Task<ActionResult<ApiResult<PagedResult<LoginFailureRecordResponse>>>> GetLoginFailuresAsync(
        [FromQuery] LoginFailureQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _securityPolicyService.GetLoginFailuresAsync(request, cancellationToken));
    }
}
