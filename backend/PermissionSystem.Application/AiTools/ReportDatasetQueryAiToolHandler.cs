using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Reports;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiTools;

public sealed class ReportDatasetQueryAiToolHandler :
    AiReadOnlyToolHandlerBase<AiReportDatasetArguments>
{
    private readonly IDataScopeService _dataScopeService;
    private readonly IReadOnlyReportQueryService _reportService;
    private readonly IRepository<ReportDefinition> _reportDefinitionRepository;
    private readonly IAiToolConfiguration _configuration;

    public ReportDatasetQueryAiToolHandler(
        IDataScopeService dataScopeService,
        IReadOnlyReportQueryService reportService,
        IRepository<ReportDefinition> reportDefinitionRepository,
        IAiToolConfiguration? configuration = null)
    {
        _dataScopeService = dataScopeService;
        _reportService = reportService;
        _reportDefinitionRepository = reportDefinitionRepository;
        _configuration = configuration ?? new DefaultAiToolConfiguration();
        Definition = new AiToolDefinition
        {
            ToolCode = "permission.reports.query_dataset",
            FunctionName = "query_approved_report_dataset",
            Version = "1.0",
            DisplayName = "Query approved report dataset",
            Description = "Query a configured report backed by an explicitly approved read-only dataset.",
            DataClassification = "Confidential",
            DataScopePolicy = AiToolDataScopePolicies.ApprovedReportDataset,
            RequiredPermissions =
            [
                AiCenterConstants.ToolQueryPermission,
                AiCenterConstants.ReportDatasetQueryPermission,
                "report:view"
            ],
            TimeoutSeconds = 60,
            MaxRows = _configuration.MaxToolRows,
            InputSchemaJson = """{"type":"object","required":["reportDefinitionId"],"properties":{"reportDefinitionId":{"type":"string","format":"uuid"},"params":{"type":"object"}},"additionalProperties":false}""",
            OutputSchemaJson = """{"type":"object","required":["columns","rows","sourceRowCount","returnedRowCount","elapsedMilliseconds","isTruncated"],"properties":{"columns":{"type":"array","items":{"type":"object","required":["key","title","type"],"properties":{"key":{"type":"string"},"title":{"type":"string"},"type":{"type":"string"}},"additionalProperties":false}},"rows":{"type":"array","items":{"type":"object"}},"sourceRowCount":{"type":"integer","minimum":0},"returnedRowCount":{"type":"integer","minimum":0},"elapsedMilliseconds":{"type":"integer","minimum":0},"isTruncated":{"type":"boolean"}},"additionalProperties":false}"""
        };
    }

    public override AiToolDefinition Definition { get; }

    public override bool IsEnabled =>
        _configuration.EnableReportDatasetTool &&
        _configuration.ApprovedReportDatasetKeys.Count > 0;

    protected override async Task<AiToolExecutionResult> ExecuteCoreAsync(
        AiToolExecutionContext context,
        AiReportDatasetArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            throw new BusinessException(ErrorCode.Forbidden, "The report dataset AI tool is disabled.");
        }

        var dataScope = await _dataScopeService.GetCurrentUserDataScopeAsync(cancellationToken);
        if (!dataScope.HasAllDataScope)
        {
            throw new BusinessException(
                ErrorCode.Forbidden,
                "The report dataset does not support the current data scope.");
        }

        var definition = await _reportDefinitionRepository.GetByIdAsync(
            arguments.ReportDefinitionId,
            cancellationToken) ?? throw new BusinessException(
                ErrorCode.NotFound,
                "The report definition was not found.");
        if (definition.TenantId != context.TenantId ||
            !definition.IsEnabled ||
            string.IsNullOrWhiteSpace(definition.DatasetKey) ||
            !_configuration.ApprovedReportDatasetKeys.Contains(
                definition.DatasetKey,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException(
                ErrorCode.Forbidden,
                "The report dataset is not approved for AI use.");
        }

        var result = await _reportService.QueryAsync(
            definition.Id,
            new ReportQueryRequest
            {
                Params = arguments.Params ?? new Dictionary<string, JsonElement>(
                    StringComparer.OrdinalIgnoreCase)
            },
            cancellationToken);
        var rows = result.Rows.Take(_configuration.MaxToolRows).ToList();
        var isTruncated = result.RowCount > rows.Count;
        return CreateResult(
            rawArguments,
            new
            {
                columns = result.Columns.Select(column => new
                {
                    column.Key,
                    column.Title,
                    column.Type
                }),
                rows,
                sourceRowCount = result.RowCount,
                returnedRowCount = rows.Count,
                result.ElapsedMilliseconds,
                isTruncated
            },
            rows.Count,
            isTruncated,
            definition.DatasetKey,
            "configured");
    }
}

public sealed class AiReportDatasetArguments
{
    public Guid ReportDefinitionId { get; init; }

    public Dictionary<string, JsonElement>? Params { get; init; }
}
