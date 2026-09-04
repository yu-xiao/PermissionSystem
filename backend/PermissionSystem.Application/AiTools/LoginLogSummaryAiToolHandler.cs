using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiTools;

public sealed class LoginLogSummaryAiToolHandler : AiReadOnlyToolHandlerBase<AiLogSummaryArguments>
{
    private readonly IRepository<LoginLog> _loginLogRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;

    public LoginLogSummaryAiToolHandler(
        IRepository<LoginLog> loginLogRepository,
        IAsyncQueryExecutor queryExecutor)
    {
        _loginLogRepository = loginLogRepository;
        _queryExecutor = queryExecutor;
    }

    public override AiToolDefinition Definition { get; } = new()
    {
        ToolCode = "permission.login_logs.summary",
        FunctionName = "summarize_login_logs",
        Version = "1.0",
        DisplayName = "Login log summary",
        Description = "Aggregate login results without returning IP addresses, user agents, or failure details.",
        DataClassification = "Confidential",
        DataScopePolicy = AiToolDataScopePolicies.CurrentTenant,
        RequiredPermissions =
        [
            AiCenterConstants.ToolQueryPermission,
            AiCenterConstants.LoginLogQueryPermission,
            "system:login-log:view"
        ],
        TimeoutSeconds = 30,
        InputSchemaJson = """{"type":"object","properties":{"userName":{"type":"string","maxLength":100},"startTime":{"type":"string","format":"date-time"},"endTime":{"type":"string","format":"date-time"}},"additionalProperties":false}""",
        OutputSchemaJson = """{"type":"object","required":["startTime","endTime","totalCount","byResult","byType"],"properties":{"startTime":{"type":"string","format":"date-time"},"endTime":{"type":"string","format":"date-time"},"totalCount":{"type":"integer","minimum":0},"byResult":{"type":"array","items":{"$ref":"#/$defs/countGroup"}},"byType":{"type":"array","items":{"$ref":"#/$defs/countGroup"}}},"$defs":{"countGroup":{"type":"object","required":["key","count"],"properties":{"key":{"type":"string"},"count":{"type":"integer","minimum":0}},"additionalProperties":false}},"additionalProperties":false}"""
    };

    protected override async Task<AiToolExecutionResult> ExecuteCoreAsync(
        AiToolExecutionContext context,
        AiLogSummaryArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        var (startTime, endTime) = NormalizeTimeRange(arguments.StartTime, arguments.EndTime);
        var query = _loginLogRepository.Query().Where(log =>
            log.TenantId == context.TenantId &&
            log.CreatedAt >= startTime &&
            log.CreatedAt <= endTime);
        if (!string.IsNullOrWhiteSpace(arguments.UserName))
        {
            var userName = NormalizeKeyword(arguments.UserName);
            query = query.Where(log => log.UserName.Contains(userName));
        }

        var totalCount = await _queryExecutor.LongCountAsync(query, cancellationToken);
        var byResult = await _queryExecutor.ToListAsync(
            query.GroupBy(log => log.LoginResult)
                .Select(group => new { key = group.Key, count = group.LongCount() })
                .OrderByDescending(item => item.count),
            cancellationToken);
        var byType = await _queryExecutor.ToListAsync(
            query.GroupBy(log => log.LoginType)
                .Select(group => new { key = group.Key, count = group.LongCount() })
                .OrderByDescending(item => item.count),
            cancellationToken);

        return CreateResult(
            rawArguments,
            new { startTime, endTime, totalCount, byResult, byType },
            checked((int)Math.Min(totalCount, int.MaxValue)),
            false);
    }
}
