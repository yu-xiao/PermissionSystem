using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.PrintTemplates;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/system")]
public sealed class PrintTemplateController : ApiControllerBase
{
    private readonly IPrintTemplateService _printTemplateService;

    public PrintTemplateController(IPrintTemplateService printTemplateService)
    {
        _printTemplateService = printTemplateService;
    }

    [HttpGet("print-templates")]
    [Permission("system:print-template:view")]
    public async Task<ActionResult<ApiResult<PagedResult<PrintTemplateResponse>>>> GetPagedAsync(
        [FromQuery] PrintTemplateQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _printTemplateService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("print-templates/{id:guid}")]
    [Permission("system:print-template:view")]
    public async Task<ActionResult<ApiResult<PrintTemplateResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _printTemplateService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost("print-templates")]
    [Permission("system:print-template:create")]
    public async Task<ActionResult<ApiResult<PrintTemplateResponse>>> CreateAsync(
        [FromBody] CreatePrintTemplateRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _printTemplateService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("print-templates/{id:guid}")]
    [Permission("system:print-template:update")]
    public async Task<ActionResult<ApiResult<PrintTemplateResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdatePrintTemplateRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _printTemplateService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("print-templates/{id:guid}")]
    [Permission("system:print-template:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _printTemplateService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpGet("print-templates/by-business-type/{businessType}")]
    [Permission("system:print-template:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<PrintTemplateResponse>>>> GetByBusinessTypeAsync(
        string businessType,
        CancellationToken cancellationToken)
    {
        return Success(await _printTemplateService.GetByBusinessTypeAsync(businessType, cancellationToken));
    }

    [HttpPost("print-templates/{id:guid}/set-default")]
    [Permission("system:print-template:update")]
    public async Task<ActionResult<ApiResult>> SetDefaultAsync(Guid id, CancellationToken cancellationToken)
    {
        await _printTemplateService.SetDefaultAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("print-templates/{id:guid}/preview")]
    [Permission("system:print-template:preview")]
    public async Task<ActionResult<ApiResult<PrintRenderResponse>>> PreviewAsync(
        Guid id,
        [FromBody] PrintRenderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _printTemplateService.PreviewAsync(id, request, cancellationToken));
    }

    [HttpPost("print-templates/{id:guid}/render")]
    [Permission("system:print-template:print")]
    public async Task<ActionResult<ApiResult<PrintRenderResponse>>> RenderAsync(
        Guid id,
        [FromBody] PrintRenderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _printTemplateService.RenderAsync(id, request, cancellationToken));
    }

    [HttpGet("print-records")]
    [Permission("system:print-record:view")]
    public async Task<ActionResult<ApiResult<PagedResult<PrintRecordResponse>>>> GetRecordsAsync(
        [FromQuery] PrintRecordQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _printTemplateService.GetRecordsAsync(request, cancellationToken));
    }
}
