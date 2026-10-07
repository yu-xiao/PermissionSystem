using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
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
    private readonly IReadOnlyReportQueryService _reportService;
    private readonly IRepository<ReportDefinition> _reportDefinitionRepository;
    private readonly IAiToolConfiguration _configuration;
    private readonly IAiQueryAccessGuard? _access;
    private readonly IReportDatasetCatalog? _catalog;

    public ReportDatasetQueryAiToolHandler(
        IDataScopeService dataScopeService,
        IReadOnlyReportQueryService reportService,
        IRepository<ReportDefinition> reportDefinitionRepository,
        IAiToolConfiguration? configuration = null,
        IAiQueryAccessGuard? access = null,
        IReportDatasetCatalog? catalog = null)
    {
        _access = access;
        _catalog = catalog;
        _reportService = reportService;
        _reportDefinitionRepository = reportDefinitionRepository;
        _configuration = configuration ?? new DefaultAiToolConfiguration();
        Definition = new AiToolDefinition
        {
            ToolCode = "permission.reports.query_dataset",
            FunctionName = "query_approved_report_dataset",
            Version = "2.0",
            DisplayName = "Query approved report dataset",
            Description = "Query an approved scoped user report by its explicit report ID. Rows returns safe user details; Metrics counts all matching users, enabled and disabled users before any display limit, grouped by None, IsEnabled or DepartmentId. CreatedAt filters use an inclusive start and exclusive end with explicit UTC offsets; this is current user state, not a historical snapshot. Never guess a report ID.",
            DataClassification = "Confidential",
            DataScopePolicy = AiToolDataScopePolicies.ApprovedReportDataset,
            RequiredPermissions =
            [
                AiCenterConstants.ToolQueryPermission,
                AiCenterConstants.ReportDatasetQueryPermission,
                "report:view", "system:user:view"
            ],
            TimeoutSeconds = 60,
            MaxRows = _configuration.MaxToolRows,
            InputSchemaJson = """{"type":"object","required":["reportDefinitionId"],"properties":{"reportDefinitionId":{"type":"string","format":"uuid"},"mode":{"type":"string","enum":["Rows","Metrics"]},"dimension":{"type":"string","enum":["None","IsEnabled","DepartmentId"]},"sort":{"type":"string","enum":["UserName","Key","CountDescending"]},"limit":{"type":"integer","minimum":1,"maximum":200},"params":{"type":"object","properties":{"keyword":{"type":["string","null"],"maxLength":100},"isEnabled":{"type":["boolean","null"]},"departmentId":{"type":["string","null"],"format":"uuid"},"departmentScope":{"type":"string","enum":["Authorized","CurrentDepartment"]},"startTime":{"type":["string","null"],"format":"date-time"},"endTime":{"type":["string","null"],"format":"date-time"}},"additionalProperties":false}},"additionalProperties":false}""",
            OutputSchemaJson = """{"type":"object","required":["sourceRowCount","returnedRowCount","totalCount","isTruncated","evaluationBasis","limitations"],"properties":{"sourceRowCount":{"type":"integer","minimum":0,"description":"Rows returned by the report service, not the full total."},"returnedRowCount":{"type":"integer","minimum":0},"totalCount":{"type":"integer","minimum":0},"table":{"type":["object","null"]},"metrics":{"type":["object","null"]},"queriedAt":{"type":"string","format":"date-time"},"evaluationBasis":{"type":"string"},"limitations":{"type":"array","items":{"type":"string"}},"elapsedMilliseconds":{"type":"integer"},"isTruncated":{"type":"boolean"}},"additionalProperties":false}"""
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

        var actor = await (_access ?? throw new BusinessException(ErrorCode.Forbidden, "AI report authorization is unavailable."))
            .AuthorizeAsync(Definition.ToolCode, cancellationToken);
        if (actor.TenantId != context.TenantId || actor.UserId != context.ActorUserId || arguments.ReportDefinitionId == Guid.Empty)
            throw new BusinessException(ErrorCode.Forbidden, "Invalid AI report query context.");

        var definition = await _reportDefinitionRepository.GetByIdAsync(
            arguments.ReportDefinitionId,
            cancellationToken) ?? throw new BusinessException(
                ErrorCode.NotFound,
                "The report definition was not found.");
        if (definition.IsDeleted || definition.TenantId != context.TenantId ||
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
        var dataset = (_catalog ?? throw new BusinessException(ErrorCode.Forbidden, "AI report catalog is unavailable."))
            .GetRequired(definition.DatasetKey!);
        if (dataset.Capability != ReportDatasetCapabilities.UserDirectory)
            throw new BusinessException(ErrorCode.Forbidden, "The dataset has no approved model-safe field and scope contract.");
        var limit = ValidateLimit(arguments.Limit, _configuration.MaxToolRows);
        var sort = ReportDatasetCapabilities.ValidateQuery(arguments.Mode, arguments.Dimension, arguments.Sort, limit);

        var result = await _reportService.QueryAsync(
            definition.Id,
            new ReportQueryRequest
            {
                Params = arguments.Params ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase),
                Mode = arguments.Mode, Dimension = arguments.Dimension, Sort = sort, Limit = limit
            },
            cancellationToken);
        var latestActor = await _access.AuthorizeAsync(Definition.ToolCode, cancellationToken);
        var latestScope = await _access.GetUserScopeAsync(latestActor, cancellationToken);
        if (!IsEnabled || !_configuration.ApprovedReportDatasetKeys.Contains(dataset.Key, StringComparer.OrdinalIgnoreCase) ||
            result.DataScopeFingerprint != AiStructuredResults.ScopeFingerprint(latestScope))
            throw new BusinessException(ErrorCode.Forbidden, "AI report access changed during execution; please retry.");
        if (result.DatasetKey != dataset.Key || result.DatasetVersion != ReportDatasetCapabilities.UserVersion ||
            result.TotalCount is not long totalCount || totalCount < 0 || result.EffectiveFilters is null ||
            string.IsNullOrWhiteSpace(result.DataScopeFingerprint) ||
            (arguments.Mode == "Metrics" ? result.Metrics is null : result.Metrics is not null))
            throw new BusinessException(ErrorCode.BusinessError, "The controlled report returned an incompatible result.");
        var table = arguments.Mode != "Rows" ? null : new AiUserTableData
        {
            TotalCount = totalCount,
            Items = result.Rows.Take(limit).Select(row => new AiUserTableRow
            {
                Id = (Guid)row["Id"]!, DepartmentId = (Guid?)row["DepartmentId"],
                UserName = (string)row["UserName"]!, DisplayName = (string)row["DisplayName"]!,
                IsEnabled = (bool)row["IsEnabled"]!, CreatedAt = (DateTimeOffset)row["CreatedAt"]!
            }).ToList()
        };
        var metrics = result.Metrics;
        if (metrics is not null && metrics.Groups.Count > limit) metrics.Groups.RemoveRange(limit, metrics.Groups.Count - limit);
        var count = table?.DisplayedRowCount ?? metrics?.DisplayedGroupCount ?? 0;
        var isTruncated = result.IsTruncated || table is not null && totalCount > table.DisplayedRowCount || metrics?.IsTruncated == true;
        var report = new AiReportResultMetadata
        {
            ReportDefinitionId = definition.Id, DatasetKey = dataset.Key, DatasetVersion = ReportDatasetCapabilities.UserVersion,
            DefinitionFingerprint = AiReportResultMetadata.Fingerprint(definition)
        };
        return CreateResult(
            rawArguments,
            new
            {
                sourceRowCount = result.RowCount,
                returnedRowCount = count, totalCount, table, metrics, result.QueriedAt, result.ElapsedMilliseconds, isTruncated,
                evaluationBasis = ReportDatasetCapabilities.EvaluationBasis,
                limitations = new[] { ReportDatasetCapabilities.DataTimeLimitation }
            },
            count,
            isTruncated,
            dataset.Key,
            ReportDatasetCapabilities.UserVersion,
            queryContext: new()
            {
                Parameters = AiStructuredResults.Parameters(new { reportDefinitionId = definition.Id, mode = arguments.Mode,
                    dimension = arguments.Dimension, sort, limit, @params = result.EffectiveFilters }),
                DataScopeFingerprint = result.DataScopeFingerprint
            },
            evaluationBasis: ReportDatasetCapabilities.EvaluationBasis, table: table, metrics: metrics, report: report,
            queriedAtOverride: result.QueriedAt);
    }
}

public sealed class AiReportDatasetArguments
{
    public Guid ReportDefinitionId { get; init; }

    public Dictionary<string, JsonElement>? Params { get; init; }
    public string Mode { get; init; } = "Rows";
    public string Dimension { get; init; } = "None";
    public string? Sort { get; init; }
    public int? Limit { get; init; }
}
