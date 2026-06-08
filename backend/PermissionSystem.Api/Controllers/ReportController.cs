using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Reports;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/reports")]
public sealed class ReportController : ApiControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    [Permission("report:definition:view|report:view")]
    public async Task<ActionResult<ApiResult<PagedResult<ReportDefinitionResponse>>>> GetPagedAsync(
        [FromQuery] ReportDefinitionQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _reportService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Permission("report:definition:view|report:view")]
    public async Task<ActionResult<ApiResult<ReportDefinitionResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _reportService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission("report:definition:create")]
    public async Task<ActionResult<ApiResult<ReportDefinitionResponse>>> CreateAsync(
        [FromBody] CreateReportDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _reportService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("report:definition:update")]
    public async Task<ActionResult<ApiResult<ReportDefinitionResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateReportDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _reportService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("report:definition:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _reportService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpPost("{id:guid}/query")]
    [Permission("report:view")]
    public async Task<ActionResult<ApiResult<ReportQueryResponse>>> QueryAsync(
        Guid id,
        [FromBody] ReportQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _reportService.QueryAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/export")]
    [Permission("report:export")]
    public async Task<IActionResult> ExportAsync(
        Guid id,
        [FromBody] ReportQueryRequest request,
        CancellationToken cancellationToken)
    {
        var content = await _reportService.ExportAsync(id, request, cancellationToken);
        var fileName = $"report-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx";
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet("execution-logs")]
    [Permission("report:log:view")]
    public async Task<ActionResult<ApiResult<PagedResult<ReportExecutionLogResponse>>>> GetExecutionLogsAsync(
        [FromQuery] ReportExecutionLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _reportService.GetExecutionLogsAsync(request, cancellationToken));
    }
}
