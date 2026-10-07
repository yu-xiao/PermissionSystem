using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai")]
public sealed class AiOperationsController : ApiControllerBase
{
    private readonly IAiOperationsService _operationsService;
    private readonly IAiScenarioOperationsService _scenarioOperationsService;
    private readonly IAiTechnicalExportService _technicalExportService;
    private readonly IAiTechnicalExportReceiptService _technicalExportReceiptService;
    private readonly IAiCostQualityService _costQualityService;

    public AiOperationsController(IAiOperationsService operationsService, IAiScenarioOperationsService scenarioOperationsService,
        IAiTechnicalExportService technicalExportService, IAiTechnicalExportReceiptService technicalExportReceiptService,
        IAiCostQualityService costQualityService)
    {
        _operationsService = operationsService;
        _scenarioOperationsService = scenarioOperationsService;
        _technicalExportService = technicalExportService;
        _technicalExportReceiptService = technicalExportReceiptService;
        _costQualityService = costQualityService;
    }

    [HttpGet("runs/{runId:guid}/feedback")]
    [Permission(AiCenterConstants.ChatUsePermission)]
    public async Task<ActionResult<ApiResult<AiFeedbackResponse?>>> GetFeedbackAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        return Success(await _operationsService.GetMyFeedbackAsync(runId, cancellationToken));
    }

    [HttpPut("runs/{runId:guid}/feedback")]
    [Permission(AiCenterConstants.ChatUsePermission)]
    public async Task<ActionResult<ApiResult<AiFeedbackResponse>>> SaveFeedbackAsync(
        Guid runId,
        [FromBody] SaveAiFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _operationsService.SaveMyFeedbackAsync(runId, request, cancellationToken));
    }

    [HttpGet("operations/summary")]
    [Permission(AiCenterConstants.OperationsViewPermission)]
    public async Task<ActionResult<ApiResult<AiOperationsSummaryResponse>>> GetSummaryAsync(
        [FromQuery] AiOperationsQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _operationsService.GetSummaryAsync(request, cancellationToken));
    }

    [HttpGet("operations/scenarios")]
    [Permission(AiCenterConstants.OperationsViewPermission)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResult<AiScenarioOperationsResponse>>> GetScenarioStatisticsAsync(
        [FromQuery] AiScenarioOperationsQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _scenarioOperationsService.QueryAsync(request, cancellationToken));
    }

    [HttpGet("operations/cost-quality")]
    [Permission(AiCenterConstants.OperationsViewPermission)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResult<AiCostQualityResponse>>> GetCostQualityAsync(
        [FromQuery] AiCostQualityQuery request, CancellationToken cancellationToken)
    {
        return Success(await _costQualityService.QueryAsync(request, cancellationToken));
    }

    [HttpGet("operations/cost-quality/trends")]
    [Permission(AiCenterConstants.OperationsViewPermission)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResult<AiCostQualityTrendResponse>>> GetCostQualityTrendsAsync(
        [FromQuery] AiCostQualityTrendQuery request, CancellationToken cancellationToken)
    {
        return Success(await _costQualityService.QueryTrendAsync(request, cancellationToken));
    }

    [HttpPost("operations/technical-export")]
    [Permission(AiCenterConstants.OperationsViewPermission)]
    [Permission(AiCenterConstants.OperationsExportPermission)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ExportTechnicalMetadataAsync([FromBody] AiTechnicalExportRequest request,
        CancellationToken cancellationToken)
    {
        var file = await _technicalExportService.ExportAsync(request, cancellationToken);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";
        return File(file.Content, "application/json; charset=utf-8", file.FileName);
    }

    [HttpGet("operations/technical-export-receipts")]
    [Permission(AiCenterConstants.OperationsViewPermission)]
    [Permission(AiCenterConstants.OperationsExportPermission)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResult<AiTechnicalExportReceiptPage>>> GetTechnicalExportReceiptsAsync(
        [FromQuery] AiTechnicalExportReceiptQuery request, CancellationToken cancellationToken)
    {
        return Success(await _technicalExportReceiptService.QueryAsync(request, cancellationToken));
    }

    [HttpGet("operations/technical-export-receipts/{exportId:guid}")]
    [Permission(AiCenterConstants.OperationsViewPermission)]
    [Permission(AiCenterConstants.OperationsExportPermission)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResult<AiTechnicalExportReceiptDetail>>> GetTechnicalExportReceiptAsync(
        Guid exportId, [FromQuery] AiTechnicalExportReceiptWindow request, CancellationToken cancellationToken)
    {
        return Success(await _technicalExportReceiptService.GetAsync(exportId, request, cancellationToken));
    }
}
