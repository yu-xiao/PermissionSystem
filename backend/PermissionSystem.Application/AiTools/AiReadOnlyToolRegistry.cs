using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Departments;
using PermissionSystem.Application.Reports;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiTools;

public sealed class AiReadOnlyToolRegistry : IAiReadOnlyToolRegistry
{
    private const string ToolVersion = "1.0";
    private const string UsersTool = "permission.users.search";
    private const string DepartmentsTool = "permission.departments.search";
    private const string RolesTool = "permission.roles.summary";
    private const string LoginLogsTool = "permission.login_logs.summary";
    private const string OperationLogsTool = "permission.operation_logs.summary";
    private const string ReportDatasetTool = "permission.reports.query_dataset";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ToolRegistration[] Registrations =
    [
        new(
            new AiToolDefinition
            {
                ToolCode = UsersTool,
                Version = ToolVersion,
                DisplayName = "Search users",
                Description = "Search non-sensitive user summaries within the current user's data scope.",
                DataClassification = "Internal",
                InputSchemaJson = """{"type":"object","properties":{"keyword":{"type":"string","maxLength":100},"isEnabled":{"type":"boolean"},"limit":{"type":"integer","minimum":1,"maximum":200}},"additionalProperties":false}"""
            },
            [AiCenterConstants.ToolQueryPermission, AiCenterConstants.UserQueryPermission, "system:user:view"]),
        new(
            new AiToolDefinition
            {
                ToolCode = DepartmentsTool,
                Version = ToolVersion,
                DisplayName = "Search departments",
                Description = "Search department summaries in the current tenant.",
                DataClassification = "Internal",
                InputSchemaJson = """{"type":"object","properties":{"keyword":{"type":"string","maxLength":100},"isEnabled":{"type":"boolean"},"limit":{"type":"integer","minimum":1,"maximum":200}},"additionalProperties":false}"""
            },
            [AiCenterConstants.ToolQueryPermission, AiCenterConstants.DepartmentQueryPermission, "system:department:view"]),
        new(
            new AiToolDefinition
            {
                ToolCode = RolesTool,
                Version = ToolVersion,
                DisplayName = "Role summary",
                Description = "Return non-sensitive role summaries in the current tenant.",
                DataClassification = "Internal",
                InputSchemaJson = """{"type":"object","properties":{"keyword":{"type":"string","maxLength":100},"isEnabled":{"type":"boolean"},"limit":{"type":"integer","minimum":1,"maximum":200}},"additionalProperties":false}"""
            },
            [AiCenterConstants.ToolQueryPermission, AiCenterConstants.RoleQueryPermission, "system:role:view"]),
        new(
            new AiToolDefinition
            {
                ToolCode = LoginLogsTool,
                Version = ToolVersion,
                DisplayName = "Login log summary",
                Description = "Aggregate login results without returning IP addresses, user agents, or failure details.",
                DataClassification = "Confidential",
                InputSchemaJson = """{"type":"object","properties":{"userName":{"type":"string","maxLength":100},"startTime":{"type":"string","format":"date-time"},"endTime":{"type":"string","format":"date-time"}},"additionalProperties":false}"""
            },
            [AiCenterConstants.ToolQueryPermission, AiCenterConstants.LoginLogQueryPermission, "system:login-log:view"]),
        new(
            new AiToolDefinition
            {
                ToolCode = OperationLogsTool,
                Version = ToolVersion,
                DisplayName = "Operation log summary",
                Description = "Aggregate operation status and modules without returning request or response bodies.",
                DataClassification = "Confidential",
                InputSchemaJson = """{"type":"object","properties":{"userName":{"type":"string","maxLength":100},"module":{"type":"string","maxLength":100},"startTime":{"type":"string","format":"date-time"},"endTime":{"type":"string","format":"date-time"}},"additionalProperties":false}"""
            },
            [AiCenterConstants.ToolQueryPermission, AiCenterConstants.OperationLogQueryPermission, "system:operation-log:view"]),
        new(
            new AiToolDefinition
            {
                ToolCode = ReportDatasetTool,
                Version = ToolVersion,
                DisplayName = "Query approved report dataset",
                Description = "Query a configured report backed by an explicitly approved read-only dataset.",
                DataClassification = "Confidential",
                InputSchemaJson = """{"type":"object","required":["reportDefinitionId"],"properties":{"reportDefinitionId":{"type":"string","format":"uuid"},"params":{"type":"object"}},"additionalProperties":false}"""
            },
            [AiCenterConstants.ToolQueryPermission, AiCenterConstants.ReportDatasetQueryPermission, "report:view"],
            true)
    ];

    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantContext _tenantContext;
    private readonly IDataScopeService _dataScopeService;
    private readonly IDataPermissionFilter _dataPermissionFilter;
    private readonly IDepartmentService _departmentService;
    private readonly IReadOnlyReportQueryService _reportService;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<LoginLog> _loginLogRepository;
    private readonly IRepository<OperationLog> _operationLogRepository;
    private readonly IRepository<ReportDefinition> _reportDefinitionRepository;
    private readonly IAsyncQueryExecutor _asyncQueryExecutor;
    private readonly IAiToolConfiguration _configuration;

    public AiReadOnlyToolRegistry(
        ICurrentUserService currentUserService,
        ITenantContext tenantContext,
        IDataScopeService dataScopeService,
        IDataPermissionFilter dataPermissionFilter,
        IDepartmentService departmentService,
        IReadOnlyReportQueryService reportService,
        IRepository<User> userRepository,
        IRepository<Role> roleRepository,
        IRepository<LoginLog> loginLogRepository,
        IRepository<OperationLog> operationLogRepository,
        IRepository<ReportDefinition> reportDefinitionRepository,
        IAsyncQueryExecutor asyncQueryExecutor,
        IAiToolConfiguration? configuration = null)
    {
        _currentUserService = currentUserService;
        _tenantContext = tenantContext;
        _dataScopeService = dataScopeService;
        _dataPermissionFilter = dataPermissionFilter;
        _departmentService = departmentService;
        _reportService = reportService;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _loginLogRepository = loginLogRepository;
        _operationLogRepository = operationLogRepository;
        _reportDefinitionRepository = reportDefinitionRepository;
        _asyncQueryExecutor = asyncQueryExecutor;
        _configuration = configuration ?? new DefaultAiToolConfiguration();
    }

    public IReadOnlyList<AiToolDefinition> GetAvailableTools()
    {
        if (!HasValidIdentity())
        {
            return [];
        }

        return Registrations
            .Where(IsAvailable)
            .Select(registration => registration.Definition)
            .ToList();
    }

    public async Task<AiToolExecutionResult> ExecuteAsync(
        string toolCode,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureValidIdentity();
        var registration = Registrations.FirstOrDefault(item =>
            string.Equals(item.Definition.ToolCode, toolCode, StringComparison.Ordinal));
        if (registration is null || !IsAvailable(registration))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The requested AI tool is not available.");
        }

        var normalizedArguments = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson.Trim();
        try
        {
            return toolCode switch
            {
                UsersTool => await SearchUsersAsync(Deserialize<SearchArguments>(normalizedArguments), normalizedArguments, cancellationToken),
                DepartmentsTool => await SearchDepartmentsAsync(Deserialize<SearchArguments>(normalizedArguments), normalizedArguments, cancellationToken),
                RolesTool => await SummarizeRolesAsync(Deserialize<SearchArguments>(normalizedArguments), normalizedArguments, cancellationToken),
                LoginLogsTool => await SummarizeLoginLogsAsync(Deserialize<LogSummaryArguments>(normalizedArguments), normalizedArguments, cancellationToken),
                OperationLogsTool => await SummarizeOperationLogsAsync(Deserialize<OperationLogSummaryArguments>(normalizedArguments), normalizedArguments, cancellationToken),
                ReportDatasetTool => await QueryReportDatasetAsync(Deserialize<ReportDatasetArguments>(normalizedArguments), normalizedArguments, cancellationToken),
                _ => throw new BusinessException(ErrorCode.NotFound, "The requested AI tool was not found.")
            };
        }
        catch (JsonException exception)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI tool arguments are invalid.", exception);
        }
    }

    private async Task<AiToolExecutionResult> SearchUsersAsync(
        SearchArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        var limit = ValidateLimit(arguments.Limit);
        var dataScope = await _dataScopeService.GetCurrentUserDataScopeAsync(cancellationToken);
        var query = _userRepository.Query().ApplyDataPermission(
            _dataPermissionFilter,
            dataScope,
            user => (Guid?)user.Id,
            user => user.DepartmentId);

        if (!string.IsNullOrWhiteSpace(arguments.Keyword))
        {
            var keyword = NormalizeKeyword(arguments.Keyword);
            query = query.Where(user => user.UserName.Contains(keyword) || user.DisplayName.Contains(keyword));
        }

        if (arguments.IsEnabled.HasValue)
        {
            query = query.Where(user => user.IsEnabled == arguments.IsEnabled.Value);
        }

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var items = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderBy(user => user.UserName)
                .Take(limit)
                .Select(user => new
                {
                    user.Id,
                    user.UserName,
                    user.DisplayName,
                    user.DepartmentId,
                    user.IsEnabled,
                    user.CreatedAt
                }),
            cancellationToken);

        return CreateResult(UsersTool, rawArguments, new { totalCount, items }, items.Count, totalCount > items.Count);
    }

    private async Task<AiToolExecutionResult> SearchDepartmentsAsync(
        SearchArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        var limit = ValidateLimit(arguments.Limit);
        var departments = Flatten(await _departmentService.GetTreeAsync(null, cancellationToken));
        IEnumerable<DepartmentTreeResponse> query = departments;
        if (!string.IsNullOrWhiteSpace(arguments.Keyword))
        {
            var keyword = NormalizeKeyword(arguments.Keyword);
            query = query.Where(item => item.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (arguments.IsEnabled.HasValue)
        {
            query = query.Where(item => item.IsEnabled == arguments.IsEnabled.Value);
        }

        var matched = query.OrderBy(item => item.TreePath).ThenBy(item => item.Sort).ToList();
        var items = matched.Take(limit).Select(item => new
        {
            item.Id,
            item.ParentId,
            item.Code,
            item.Name,
            item.IsEnabled
        }).ToList();

        return CreateResult(DepartmentsTool, rawArguments, new { totalCount = matched.Count, items }, items.Count, matched.Count > items.Count);
    }

    private async Task<AiToolExecutionResult> SummarizeRolesAsync(
        SearchArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        var limit = ValidateLimit(arguments.Limit);
        var query = _roleRepository.Query();
        if (!string.IsNullOrWhiteSpace(arguments.Keyword))
        {
            var keyword = NormalizeKeyword(arguments.Keyword);
            query = query.Where(role => role.Code.Contains(keyword) || role.Name.Contains(keyword));
        }

        if (arguments.IsEnabled.HasValue)
        {
            query = query.Where(role => role.IsEnabled == arguments.IsEnabled.Value);
        }

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var items = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderBy(role => role.Sort)
                .ThenBy(role => role.Code)
                .Take(limit)
                .Select(role => new { role.Id, role.Code, role.Name, role.IsEnabled }),
            cancellationToken);

        return CreateResult(RolesTool, rawArguments, new { totalCount, items }, items.Count, totalCount > items.Count);
    }

    private async Task<AiToolExecutionResult> SummarizeLoginLogsAsync(
        LogSummaryArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        var (startTime, endTime) = NormalizeTimeRange(arguments.StartTime, arguments.EndTime);
        var query = _loginLogRepository.Query().Where(log => log.CreatedAt >= startTime && log.CreatedAt <= endTime);
        if (!string.IsNullOrWhiteSpace(arguments.UserName))
        {
            var userName = NormalizeKeyword(arguments.UserName);
            query = query.Where(log => log.UserName.Contains(userName));
        }

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var byResult = await _asyncQueryExecutor.ToListAsync(
            query.GroupBy(log => log.LoginResult)
                .Select(group => new { key = group.Key, count = group.LongCount() })
                .OrderByDescending(item => item.count),
            cancellationToken);
        var byType = await _asyncQueryExecutor.ToListAsync(
            query.GroupBy(log => log.LoginType)
                .Select(group => new { key = group.Key, count = group.LongCount() })
                .OrderByDescending(item => item.count),
            cancellationToken);

        return CreateResult(LoginLogsTool, rawArguments, new { startTime, endTime, totalCount, byResult, byType }, checked((int)Math.Min(totalCount, int.MaxValue)), false);
    }

    private async Task<AiToolExecutionResult> SummarizeOperationLogsAsync(
        OperationLogSummaryArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        var (startTime, endTime) = NormalizeTimeRange(arguments.StartTime, arguments.EndTime);
        var query = _operationLogRepository.Query().Where(log => log.CreatedAt >= startTime && log.CreatedAt <= endTime);
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

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var byStatus = await _asyncQueryExecutor.ToListAsync(
            query.GroupBy(log => log.StatusCode)
                .Select(group => new { key = group.Key, count = group.LongCount() })
                .OrderBy(item => item.key),
            cancellationToken);
        var byModule = await _asyncQueryExecutor.ToListAsync(
            query.GroupBy(log => log.Module)
                .Select(group => new { key = group.Key, count = group.LongCount() })
                .OrderByDescending(item => item.count)
                .Take(20),
            cancellationToken);

        return CreateResult(OperationLogsTool, rawArguments, new { startTime, endTime, totalCount, byStatus, byModule }, checked((int)Math.Min(totalCount, int.MaxValue)), false);
    }

    private async Task<AiToolExecutionResult> QueryReportDatasetAsync(
        ReportDatasetArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        if (!_configuration.EnableReportDatasetTool)
        {
            throw new BusinessException(ErrorCode.Forbidden, "The report dataset AI tool is disabled.");
        }

        var dataScope = await _dataScopeService.GetCurrentUserDataScopeAsync(cancellationToken);
        if (!dataScope.HasAllDataScope)
        {
            throw new BusinessException(ErrorCode.Forbidden, "The report dataset does not support the current data scope.");
        }

        var definition = await _reportDefinitionRepository.GetByIdAsync(arguments.ReportDefinitionId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "The report definition was not found.");
        if (!definition.IsEnabled || string.IsNullOrWhiteSpace(definition.DatasetKey) ||
            !_configuration.ApprovedReportDatasetKeys.Contains(definition.DatasetKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The report dataset is not approved for AI use.");
        }

        var result = await _reportService.QueryAsync(
            definition.Id,
            new ReportQueryRequest { Params = arguments.Params ?? new(StringComparer.OrdinalIgnoreCase) },
            cancellationToken);
        var rows = result.Rows.Take(_configuration.MaxToolRows).ToList();
        var isTruncated = result.RowCount > rows.Count;
        return CreateResult(
            ReportDatasetTool,
            rawArguments,
            new
            {
                columns = result.Columns.Select(column => new { column.Key, column.Title, column.Type }),
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

    private bool IsAvailable(ToolRegistration registration)
    {
        if (registration.RequiresApprovedReportDataset &&
            (!_configuration.EnableReportDatasetTool || _configuration.ApprovedReportDatasetKeys.Count == 0))
        {
            return false;
        }

        return registration.RequiredPermissions.All(_currentUserService.HasPermission);
    }

    private bool HasValidIdentity()
    {
        return _currentUserService.IsAuthenticated &&
            _currentUserService.UserId.HasValue &&
            _currentUserService.TenantId.HasValue &&
            _tenantContext.TenantId.HasValue &&
            _currentUserService.TenantId.Value == _tenantContext.TenantId.Value;
    }

    private void EnsureValidIdentity()
    {
        if (!_currentUserService.IsAuthenticated)
        {
            throw new BusinessException(ErrorCode.Unauthorized, "Authentication is required.");
        }

        if (!HasValidIdentity())
        {
            throw new BusinessException(ErrorCode.Forbidden, "The AI tool tenant context is invalid.");
        }
    }

    private int ValidateLimit(int? requestedLimit)
    {
        var limit = requestedLimit ?? Math.Min(20, _configuration.MaxToolRows);
        if (limit is < 1 || limit > _configuration.MaxToolRows)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, $"Tool limit must be between 1 and {_configuration.MaxToolRows}.");
        }

        return limit;
    }

    private static string NormalizeKeyword(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length > 100)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Tool keyword is too long.");
        }

        return normalized;
    }

    private static (DateTimeOffset StartTime, DateTimeOffset EndTime) NormalizeTimeRange(
        DateTimeOffset? requestedStart,
        DateTimeOffset? requestedEnd)
    {
        var endTime = requestedEnd ?? DateTimeOffset.UtcNow;
        var startTime = requestedStart ?? endTime.AddDays(-7);
        if (startTime > endTime || endTime - startTime > TimeSpan.FromDays(31))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Tool time range must be valid and no longer than 31 days.");
        }

        return (startTime, endTime);
    }

    private static T Deserialize<T>(string json)
        where T : class, new()
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new BusinessException(ErrorCode.ValidationFailed, "AI tool arguments are required.");
    }

    private static IReadOnlyList<DepartmentTreeResponse> Flatten(IReadOnlyList<DepartmentTreeResponse> roots)
    {
        var result = new List<DepartmentTreeResponse>();
        var stack = new Stack<DepartmentTreeResponse>(roots.Reverse());
        while (stack.TryPop(out var current))
        {
            result.Add(current);
            foreach (var child in current.Children.Reverse())
            {
                stack.Push(child);
            }
        }

        return result;
    }

    private static AiToolExecutionResult CreateResult(
        string toolCode,
        string rawArguments,
        object data,
        int rowCount,
        bool isTruncated,
        string? datasetCode = null,
        string? datasetVersion = null)
    {
        var queriedAt = DateTimeOffset.UtcNow;
        return new AiToolExecutionResult
        {
            ContentJson = JsonSerializer.Serialize(data, JsonOptions),
            RowCount = rowCount,
            IsTruncated = isTruncated,
            Citation = new AiToolCitation
            {
                ToolCode = toolCode,
                ToolVersion = ToolVersion,
                DatasetCode = datasetCode,
                DatasetVersion = datasetVersion,
                QueryParametersDigest = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(rawArguments))),
                QueriedAt = queriedAt,
                AsOf = queriedAt,
                RowCount = rowCount
            }
        };
    }

    private sealed record ToolRegistration(
        AiToolDefinition Definition,
        IReadOnlyCollection<string> RequiredPermissions,
        bool RequiresApprovedReportDataset = false);

    private sealed class SearchArguments
    {
        public string? Keyword { get; init; }

        public bool? IsEnabled { get; init; }

        public int? Limit { get; init; }
    }

    private class LogSummaryArguments
    {
        public string? UserName { get; init; }

        public DateTimeOffset? StartTime { get; init; }

        public DateTimeOffset? EndTime { get; init; }
    }

    private sealed class OperationLogSummaryArguments : LogSummaryArguments
    {
        public string? Module { get; init; }
    }

    private sealed class ReportDatasetArguments
    {
        public Guid ReportDefinitionId { get; init; }

        public Dictionary<string, JsonElement>? Params { get; init; }
    }
}
