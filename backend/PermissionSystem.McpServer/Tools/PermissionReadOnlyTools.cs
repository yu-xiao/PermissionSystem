using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PermissionSystem.Application.AiTools;

namespace PermissionSystem.McpServer.Tools;

[McpServerToolType]
public sealed class PermissionReadOnlyTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAiReadOnlyToolRegistry _registry;

    public PermissionReadOnlyTools(IAiReadOnlyToolRegistry registry)
    {
        _registry = registry;
    }

    [McpServerTool(Name = "search_users", UseStructuredContent = true)]
    [Description("Searches non-sensitive user summaries within the authenticated caller's tenant and data scope.")]
    public Task<AiToolExecutionResult> SearchUsersAsync(
        string? keyword = null,
        bool? isEnabled = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            "permission.users.search",
            new { keyword, isEnabled, limit },
            cancellationToken);
    }

    [McpServerTool(Name = "search_departments", UseStructuredContent = true)]
    [Description("Searches department summaries in the authenticated caller's tenant.")]
    public Task<AiToolExecutionResult> SearchDepartmentsAsync(
        string? keyword = null,
        bool? isEnabled = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            "permission.departments.search",
            new { keyword, isEnabled, limit },
            cancellationToken);
    }

    [McpServerTool(Name = "summarize_roles", UseStructuredContent = true)]
    [Description("Returns non-sensitive role summaries in the authenticated caller's tenant.")]
    public Task<AiToolExecutionResult> SummarizeRolesAsync(
        string? keyword = null,
        bool? isEnabled = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            "permission.roles.summary",
            new { keyword, isEnabled, limit },
            cancellationToken);
    }

    [McpServerTool(Name = "summarize_login_logs", UseStructuredContent = true)]
    [Description("Aggregates login results without returning IP addresses, user agents, or failure details.")]
    public Task<AiToolExecutionResult> SummarizeLoginLogsAsync(
        string? userName = null,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            "permission.login_logs.summary",
            new { userName, startTime, endTime },
            cancellationToken);
    }

    [McpServerTool(Name = "summarize_operation_logs", UseStructuredContent = true)]
    [Description("Aggregates operation status and modules without returning request or response bodies.")]
    public Task<AiToolExecutionResult> SummarizeOperationLogsAsync(
        string? userName = null,
        string? module = null,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            "permission.operation_logs.summary",
            new { userName, module, startTime, endTime },
            cancellationToken);
    }

    private Task<AiToolExecutionResult> ExecuteAsync(
        string toolCode,
        object arguments,
        CancellationToken cancellationToken)
    {
        return _registry.ExecuteAsync(
            toolCode,
            JsonSerializer.Serialize(arguments, JsonOptions),
            cancellationToken);
    }
}
