using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Excels;
using PermissionSystem.Application.Files;
using PermissionSystem.Application.OperationLogs;
using PermissionSystem.Application.PrintTemplates;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/demo-business-orders")]
public sealed class DemoBusinessOrderController : ApiControllerBase
{
    private readonly IDemoBusinessOrderService _demoBusinessOrderService;

    public DemoBusinessOrderController(IDemoBusinessOrderService demoBusinessOrderService)
    {
        _demoBusinessOrderService = demoBusinessOrderService;
    }

    [HttpGet]
    [Permission("demo-business-order:view")]
    public async Task<ActionResult<ApiResult<PagedResult<DemoBusinessOrderResponse>>>> GetPagedAsync(
        [FromQuery] DemoBusinessOrderQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("demo-business-order:view")]
    public async Task<ActionResult<ApiResult<DemoBusinessOrderResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("demo-business-order:create")]
    public async Task<ActionResult<ApiResult<DemoBusinessOrderResponse>>> CreateAsync(
        [FromBody] CreateDemoBusinessOrderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("demo-business-order:update")]
    public async Task<ActionResult<ApiResult<DemoBusinessOrderResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateDemoBusinessOrderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("demo-business-order:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _demoBusinessOrderService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/submit")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("demo-business-order:submit")]
    public async Task<ActionResult<ApiResult<DemoBusinessOrderResponse>>> SubmitAsync(
        Guid id,
        [FromBody] SubmitDemoBusinessOrderRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.SubmitAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/withdraw")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("demo-business-order:withdraw")]
    public async Task<ActionResult<ApiResult<DemoBusinessOrderResponse>>> WithdrawAsync(
        Guid id,
        [FromBody] WorkflowTaskActionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.WithdrawAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    [IdempotencyKey]
    [PreventDuplicateSubmit]
    [Permission("demo-business-order:cancel")]
    public async Task<ActionResult<ApiResult<DemoBusinessOrderResponse>>> CancelAsync(
        Guid id,
        [FromBody] WorkflowTaskActionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.CancelAsync(id, request, cancellationToken));
    }

    [HttpGet("export")]
    [Permission("demo-business-order:export")]
    public async Task<IActionResult> ExportAsync(
        [FromQuery] DemoBusinessOrderQueryRequest request,
        CancellationToken cancellationToken)
    {
        var content = await _demoBusinessOrderService.ExportAsync(request, cancellationToken);
        var fileName = $"demo-business-orders-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx";
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("import-template")]
    [Permission("demo-business-order:import")]
    public async Task<IActionResult> DownloadImportTemplateAsync(CancellationToken cancellationToken)
    {
        var content = await _demoBusinessOrderService.CreateImportTemplateAsync(cancellationToken);
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "demo-business-order-import-template.xlsx");
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [Permission("demo-business-order:import")]
    public async Task<ActionResult<ApiResult<ImportResult<DemoBusinessOrderImportRow>>>> ImportAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(ApiResult<ImportResult<DemoBusinessOrderImportRow>>.Fail(
                ErrorCode.ValidationFailed,
                "File is required.",
                HttpContext.TraceIdentifier));
        }

        await using var stream = file.OpenReadStream();
        return Success(await _demoBusinessOrderService.ImportPreviewAsync(stream, cancellationToken));
    }

    [HttpGet("{id:guid}/attachments")]
    [Permission("demo-business-order:attachment:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<FileResourceResponse>>>> GetAttachmentsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.GetAttachmentsAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [Permission("demo-business-order:attachment:upload")]
    public async Task<ActionResult<ApiResult<FileResourceResponse>>> UploadAttachmentAsync(
        Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(ApiResult<FileResourceResponse>.Fail(
                ErrorCode.ValidationFailed,
                "File is required.",
                HttpContext.TraceIdentifier));
        }

        await using var stream = file.OpenReadStream();
        return Success(await _demoBusinessOrderService.UploadAttachmentAsync(
            id,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            cancellationToken));
    }

    [HttpGet("print-templates")]
    [Permission("demo-business-order:print")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<PrintTemplateResponse>>>> GetPrintTemplatesAsync(
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.GetPrintTemplatesAsync(cancellationToken));
    }

    [HttpPost("{id:guid}/print/{templateId:guid}")]
    [Permission("demo-business-order:print")]
    public async Task<ActionResult<ApiResult<DemoBusinessOrderPrintResponse>>> RenderPrintAsync(
        Guid id,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.RenderPrintAsync(id, templateId, cancellationToken));
    }

    [HttpGet("{id:guid}/operation-logs")]
    [Permission("demo-business-order:log:view")]
    public async Task<ActionResult<ApiResult<PagedResult<OperationLogResponse>>>> GetOperationLogsAsync(
        Guid id,
        [FromQuery] PaginationRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.GetOperationLogsAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/change-histories")]
    [Permission("demo-business-order:history:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<DemoBusinessOrderChangeHistoryResponse>>>> GetChangeHistoriesAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _demoBusinessOrderService.GetChangeHistoriesAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/notify")]
    [Permission("demo-business-order:notify")]
    public async Task<ActionResult<ApiResult>> NotifyAsync(Guid id, CancellationToken cancellationToken)
    {
        await _demoBusinessOrderService.NotifyOwnerAsync(id, cancellationToken);
        return Success();
    }
}
