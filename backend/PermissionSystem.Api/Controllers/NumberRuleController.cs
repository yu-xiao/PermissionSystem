using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.NumberRules;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/system/number-rules")]
public sealed class NumberRuleController : ApiControllerBase
{
    private readonly INumberRuleService _numberRuleService;
    private readonly INumberGenerator _numberGenerator;

    public NumberRuleController(
        INumberRuleService numberRuleService,
        INumberGenerator numberGenerator)
    {
        _numberRuleService = numberRuleService;
        _numberGenerator = numberGenerator;
    }

    [HttpGet]
    [Permission("system:number-rule:view")]
    public async Task<ActionResult<ApiResult<PagedResult<NumberRuleResponse>>>> GetPagedAsync(
        [FromQuery] NumberRuleQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _numberRuleService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("system:number-rule:view")]
    public async Task<ActionResult<ApiResult<NumberRuleResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _numberRuleService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission("system:number-rule:create")]
    public async Task<ActionResult<ApiResult<NumberRuleResponse>>> CreateAsync(
        [FromBody] CreateOrUpdateNumberRuleRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _numberRuleService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:number-rule:update")]
    public async Task<ActionResult<ApiResult<NumberRuleResponse>>> UpdateAsync(
        Guid id,
        [FromBody] CreateOrUpdateNumberRuleRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _numberRuleService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:number-rule:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _numberRuleService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/enable")]
    [Permission("system:number-rule:enable")]
    public async Task<ActionResult<ApiResult>> EnableAsync(Guid id, CancellationToken cancellationToken)
    {
        await _numberRuleService.EnableAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/disable")]
    [Permission("system:number-rule:disable")]
    public async Task<ActionResult<ApiResult>> DisableAsync(Guid id, CancellationToken cancellationToken)
    {
        await _numberRuleService.DisableAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("preview")]
    [Permission("system:number-rule:preview")]
    public async Task<ActionResult<ApiResult<NumberRulePreviewResponse>>> PreviewAsync(
        [FromBody] CreateOrUpdateNumberRuleRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _numberRuleService.PreviewAsync(request, cancellationToken));
    }

    [HttpPost("{ruleCode}/generate")]
    [Permission("system:number-rule:generate")]
    public async Task<ActionResult<ApiResult<NumberGenerateResponse>>> GenerateAsync(
        string ruleCode,
        CancellationToken cancellationToken)
    {
        var number = await _numberGenerator.GenerateAsync(ruleCode, cancellationToken);
        return Success(new NumberGenerateResponse
        {
            RuleCode = ruleCode,
            Number = number
        });
    }

    [HttpPost("{ruleCode}/reset-sequence")]
    [Permission("system:number-rule:reset")]
    public async Task<ActionResult<ApiResult>> ResetSequenceAsync(
        string ruleCode,
        CancellationToken cancellationToken)
    {
        await _numberRuleService.ResetSequenceAsync(ruleCode, cancellationToken);
        return Success();
    }
}
