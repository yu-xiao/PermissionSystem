using PermissionSystem.Application.Abstractions;
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
        var query = _operationLogRepository.Query().Where(log =>
            log.TenantId == context.TenantId &&
            log.CreatedAt >= startTime &&
            log.CreatedAt <= endTime);
        if (!string.IsNullOrWhiteSpace(arguments.UserName))
        {
            var userName = NormalizeKeyword(arguments.UserName);
            query = query.Where(log => log.UserName != null && log.UserName.Contains(userName));
        }

        if (!string.IsNullOrWhiteSpace(arguments.Module))
        {
            var module = NormalizeKeyword(arguments.Module);
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
                .Take(20),
            cancellationToken);

        return CreateResult(
            rawArguments,
            new { startTime, endTime, totalCount, byStatus, byModule },
            checked((int)Math.Min(totalCount, int.MaxValue)),
            false);
    }
}
