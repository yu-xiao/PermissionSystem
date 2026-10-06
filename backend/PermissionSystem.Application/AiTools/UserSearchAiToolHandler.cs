using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiTools;

public sealed class UserSearchAiToolHandler : AiReadOnlyToolHandlerBase<AiUserSearchArguments>
{
    private readonly IDataScopeService _dataScopeService;
    private readonly IDataPermissionFilter _dataPermissionFilter;
    private readonly IRepository<User> _userRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly IAiToolConfiguration _configuration;
    private readonly IAiQueryAccessGuard? _accessGuard;
    private readonly IRepository<Department>? _departments;

    public UserSearchAiToolHandler(
        IDataScopeService dataScopeService,
        IDataPermissionFilter dataPermissionFilter,
        IRepository<User> userRepository,
        IAsyncQueryExecutor queryExecutor,
        IAiToolConfiguration? configuration = null,
        IAiQueryAccessGuard? accessGuard = null,
        IRepository<Department>? departments = null)
    {
        _dataScopeService = dataScopeService;
        _dataPermissionFilter = dataPermissionFilter;
        _userRepository = userRepository;
        _queryExecutor = queryExecutor;
        _configuration = configuration ?? new DefaultAiToolConfiguration();
        _accessGuard = accessGuard;
        _departments = departments;
        Definition = new AiToolDefinition
        {
            ToolCode = "permission.users.search",
            FunctionName = "search_users",
            Version = "1.0",
            DisplayName = "Search users",
            Description = "Search non-sensitive user summaries within the current user's data scope.",
            DataClassification = "Internal",
            DataScopePolicy = AiToolDataScopePolicies.CurrentUserDataScope,
            RequiredPermissions =
            [
                AiCenterConstants.ToolQueryPermission,
                AiCenterConstants.UserQueryPermission,
                "system:user:view"
            ],
            TimeoutSeconds = 30,
            MaxRows = _configuration.MaxToolRows,
            InputSchemaJson = """{"type":"object","properties":{"keyword":{"type":"string","maxLength":100},"isEnabled":{"type":"boolean"},"limit":{"type":"integer","minimum":1,"maximum":200}},"additionalProperties":false}""",
            OutputSchemaJson = """{"type":"object","required":["totalCount","items"],"properties":{"totalCount":{"type":"integer","minimum":0},"items":{"type":"array","items":{"type":"object","required":["id","userName","displayName","isEnabled","createdAt"],"properties":{"id":{"type":"string","format":"uuid"},"userName":{"type":"string"},"displayName":{"type":"string"},"departmentId":{"type":["string","null"],"format":"uuid"},"isEnabled":{"type":"boolean"},"createdAt":{"type":"string","format":"date-time"}},"additionalProperties":false}}},"additionalProperties":false}"""
        };
    }

    public override AiToolDefinition Definition { get; }

    protected override async Task<AiToolExecutionResult> ExecuteCoreAsync(
        AiToolExecutionContext context,
        AiUserSearchArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        var limit = ValidateLimit(arguments.Limit, _configuration.MaxToolRows);
        var keyword = arguments.Keyword is null ? null : NormalizeKeyword(arguments.Keyword);
        var departmentScope = arguments.DepartmentScope ?? "Authorized";
        if (departmentScope is not ("Authorized" or "CurrentDepartment"))
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid department scope.");
        var actor = _accessGuard is null ? null : await _accessGuard.AuthorizeAsync(Definition.ToolCode, cancellationToken);
        if (actor is not null && (actor.UserId != context.ActorUserId || actor.TenantId != context.TenantId))
            throw new BusinessException(ErrorCode.Forbidden, "Invalid user query context.");
        var dataScope = actor is null
            ? await _dataScopeService.GetCurrentUserDataScopeAsync(cancellationToken)
            : await _accessGuard!.GetUserScopeAsync(actor, cancellationToken);
        var query = _userRepository.Query()
            .Where(user => user.TenantId == context.TenantId && !user.IsDeleted)
            .ApplyDataPermission(
                _dataPermissionFilter,
                dataScope,
                user => (Guid?)user.Id,
                user => user.DepartmentId);

        if (departmentScope == "CurrentDepartment")
        {
            if (actor?.DepartmentId is not Guid departmentId || _departments is null ||
                !await _queryExecutor.AnyAsync(_departments.Query().Where(department =>
                    department.TenantId == context.TenantId && department.Id == departmentId &&
                    !department.IsDeleted && department.IsEnabled), cancellationToken))
                throw new BusinessException(ErrorCode.ValidationFailed, "当前部门不可用，请明确查询范围。");
            query = query.Where(user => user.DepartmentId == departmentId);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(user =>
                user.UserName.Contains(keyword) || user.DisplayName.Contains(keyword));
        }

        if (arguments.IsEnabled.HasValue)
        {
            query = query.Where(user => user.IsEnabled == arguments.IsEnabled.Value);
        }

        var totalCount = await _queryExecutor.LongCountAsync(query, cancellationToken);
        var items = await _queryExecutor.ToListAsync(
            query.OrderBy(user => user.UserName)
                .ThenBy(user => user.Id)
                .Take(limit)
                .Select(user => new AiUserTableRow
                {
                    Id = user.Id, UserName = user.UserName, DisplayName = user.DisplayName,
                    DepartmentId = user.DepartmentId, IsEnabled = user.IsEnabled, CreatedAt = user.CreatedAt
                }),
            cancellationToken);

        return CreateResult(
            rawArguments,
            new { totalCount, items },
            items.Count,
            totalCount > items.Count,
            queryContext: new AiQueryContext
            {
                Parameters = AiStructuredResults.Parameters(new { keyword, arguments.IsEnabled, limit, departmentScope }),
                DataScopeFingerprint = AiStructuredResults.ScopeFingerprint(dataScope)
            },
            evaluationBasis: "当前租户及当前授权数据范围；关键词按用户名或显示名包含匹配；本部门仅含当前部门。",
            table: new AiUserTableData { TotalCount = totalCount, Items = items.ToList() });
    }
}
