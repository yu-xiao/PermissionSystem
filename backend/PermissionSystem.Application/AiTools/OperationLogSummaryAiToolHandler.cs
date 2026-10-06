using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiTools;

public sealed class OperationLogSummaryAiToolHandler : AiReadOnlyToolHandlerBase<AiOperationLogSummaryArguments>
{
    private readonly IRepository<OperationLog> _operationLogRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;

    public OperationLogSummaryAiToolHandler(
        IRepository<OperationLog> operationLogRepository,
        IAsyncQueryExecutor queryExecutor)
    {
        _operationLogRepository = operationLogRepository;
        _queryExecutor = queryExecutor;
    }

    public override AiToolDefinition Definition { get; } = new()
    {
        ToolCode = "permission.operation_logs.summary",
        FunctionName = "summarize_operation_logs",
        Version = "1.0",
        DisplayName = "Operation log summary",
        Description = "Aggregate operation status and modules without returning request or response bodies.",
        DataClassification = "Confidential",
        DataScopePolicy = AiToolDataScopePolicies.CurrentTenant,
        RequiredPermissions =
        [
            AiCenterConstants.ToolQueryPermission,
            AiCenterConstants.OperationLogQueryPermission,
            "system:operation-log:view"
        ],
        TimeoutSeconds = 30,
        MaxRows = 20,
        InputSchemaJson = """{"type":"object","properties":{"userName":{"type":"string","maxLength":100},"module":{"type":"string","maxLength":100},"startTime":{"type":"string","format":"date-time"},"endTime":{"type":"string","format":"date-time"}},"additionalProperties":false}""",
        OutputSchemaJson = """{"type":"object","required":["startTime","endTime","totalCount","byStatus","byModule"],"properties":{"startTime":{"type":"string","format":"date-time"},"endTime":{"type":"string","format":"date-time"},"totalCount":{"type":"integer","minimum":0},"byStatus":{"type":"array","items":{"type":"object","required":["key","count"],"properties":{"key":{"type":"integer"},"count":{"type":"integer","minimum":0}},"additionalProperties":false}},"byModule":{"type":"array","maxItems":20,"items":{"type":"object","required":["key","count"],"properties":{"key":{"type":"string"},"count":{"type":"integer","minimum":0}},"additionalProperties":false}}},"additionalProperties":false}"""
    };

    protected override async Task<AiToolExecutionResult> ExecuteCoreAsync(
        AiToolExecutionContext context,
        AiOperationLogSummaryArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        var (startTime, endTime) = NormalizeTimeRange(arguments.StartTime, arguments.EndTime);
        var userName = arguments.UserName is null ? null : NormalizeKeyword(arguments.UserName);
        var module = arguments.Module is null ? null : NormalizeKeyword(arguments.Module);
        var query = _operationLogRepository.Query().Where(log =>
            log.TenantId == context.TenantId &&
            log.CreatedAt >= startTime &&
            log.CreatedAt <= endTime);
        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(log => log.UserName != null && log.UserName.Contains(userName));
        }

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(log => log.Module.Contains(module));
        }

        var totalCount = await _queryExecutor.LongCountAsync(query, cancellationToken);
        var byStatus = await _queryExecutor.ToListAsync(
            query.GroupBy(log => log.StatusCode)
                .Select(group => new { key = group.Key, count = group.LongCount() })
                .OrderBy(item => item.key),
            cancellationToken);
        var byModule = await _queryExecutor.ToListAsync(
            query.GroupBy(log => log.Module)
                .Select(group => new { key = group.Key, count = group.LongCount() })
                .OrderByDescending(item => item.count)
                .ThenBy(item => item.key)
                .Take(20),
            cancellationToken);
        var moduleGroupCount = await _queryExecutor.LongCountAsync(query.Select(log => log.Module).Distinct(), cancellationToken);

        return CreateResult(
            rawArguments,
            new { startTime, endTime, totalCount, byStatus, byModule },
            checked((int)Math.Min(totalCount, int.MaxValue)),
            moduleGroupCount > byModule.Count,
            queryContext: new AiQueryContext { Parameters = AiStructuredResults.Parameters(new { userName, module, startTime, endTime }) },
            evaluationBasis: "当前租户；CreatedAt 时间闭区间；用户名及模块包含匹配；模块仅展示数量最多的前 20 组。",
            statistics: new AiStatisticsData
            {
                TotalCount = totalCount,
                Groups =
                [
                    new() { Code = "byStatus", TotalGroupCount = byStatus.Count, Items = byStatus.Select(item => new AiCountGroup(item.key.ToString(System.Globalization.CultureInfo.InvariantCulture), item.count)).ToList() },
                    new() { Code = "byModule", TotalGroupCount = moduleGroupCount, Items = byModule.Select(item => new AiCountGroup(item.key, item.count)).ToList() }
                ]
            });
    }
}
