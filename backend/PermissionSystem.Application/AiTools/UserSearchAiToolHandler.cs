using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiTools;

public sealed class UserSearchAiToolHandler : AiReadOnlyToolHandlerBase<AiSearchArguments>
{
    private readonly IDataScopeService _dataScopeService;
    private readonly IDataPermissionFilter _dataPermissionFilter;
    private readonly IRepository<User> _userRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly IAiToolConfiguration _configuration;

    public UserSearchAiToolHandler(
        IDataScopeService dataScopeService,
        IDataPermissionFilter dataPermissionFilter,
        IRepository<User> userRepository,
        IAsyncQueryExecutor queryExecutor,
        IAiToolConfiguration? configuration = null)
    {
        _dataScopeService = dataScopeService;
        _dataPermissionFilter = dataPermissionFilter;
        _userRepository = userRepository;
        _queryExecutor = queryExecutor;
        _configuration = configuration ?? new DefaultAiToolConfiguration();
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
        AiSearchArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken)
    {
        var limit = ValidateLimit(arguments.Limit, _configuration.MaxToolRows);
        var dataScope = await _dataScopeService.GetCurrentUserDataScopeAsync(cancellationToken);
        var query = _userRepository.Query()
            .Where(user => user.TenantId == context.TenantId)
            .ApplyDataPermission(
                _dataPermissionFilter,
                dataScope,
                user => (Guid?)user.Id,
                user => user.DepartmentId);

        if (!string.IsNullOrWhiteSpace(arguments.Keyword))
        {
            var keyword = NormalizeKeyword(arguments.Keyword);
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

        return CreateResult(
            rawArguments,
            new { totalCount, items },
            items.Count,
            totalCount > items.Count);
    }
}
