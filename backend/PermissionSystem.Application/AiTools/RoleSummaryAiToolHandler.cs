using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiTools;

public sealed class RoleSummaryAiToolHandler : AiReadOnlyToolHandlerBase<AiSearchArguments>
{
    private readonly IRepository<Role> _roleRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly IAiToolConfiguration _configuration;

    public RoleSummaryAiToolHandler(
        IRepository<Role> roleRepository,
        IAsyncQueryExecutor queryExecutor,
        IAiToolConfiguration? configuration = null)
    {
        _roleRepository = roleRepository;
        _queryExecutor = queryExecutor;
        _configuration = configuration ?? new DefaultAiToolConfiguration();
        Definition = new AiToolDefinition
        {
            ToolCode = "permission.roles.summary",
            FunctionName = "summarize_roles",
            Version = "1.0",
            DisplayName = "Role summary",
            Description = "Return non-sensitive role summaries in the current tenant.",
            DataClassification = "Internal",
            DataScopePolicy = AiToolDataScopePolicies.CurrentTenant,
            RequiredPermissions =
            [
                AiCenterConstants.ToolQueryPermission,
                AiCenterConstants.RoleQueryPermission,
                "system:role:view"
            ],
            TimeoutSeconds = 30,
            MaxRows = _configuration.MaxToolRows,
            InputSchemaJson = """{"type":"object","properties":{"keyword":{"type":"string","maxLength":100},"isEnabled":{"type":"boolean"},"limit":{"type":"integer","minimum":1,"maximum":200}},"additionalProperties":false}""",
            OutputSchemaJson = """{"type":"object","required":["totalCount","items"],"properties":{"totalCount":{"type":"integer","minimum":0},"items":{"type":"array","items":{"type":"object","required":["id","code","name","isEnabled"],"properties":{"id":{"type":"string","format":"uuid"},"code":{"type":"string"},"name":{"type":"string"},"isEnabled":{"type":"boolean"}},"additionalProperties":false}}},"additionalProperties":false}"""
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
        var query = _roleRepository.Query().Where(role => role.TenantId == context.TenantId);
        if (!string.IsNullOrWhiteSpace(arguments.Keyword))
        {
            var keyword = NormalizeKeyword(arguments.Keyword);
            query = query.Where(role => role.Code.Contains(keyword) || role.Name.Contains(keyword));
        }

        if (arguments.IsEnabled.HasValue)
        {
            query = query.Where(role => role.IsEnabled == arguments.IsEnabled.Value);
        }

        var totalCount = await _queryExecutor.LongCountAsync(query, cancellationToken);
        var items = await _queryExecutor.ToListAsync(
            query.OrderBy(role => role.Sort)
                .ThenBy(role => role.Code)
                .Take(limit)
                .Select(role => new { role.Id, role.Code, role.Name, role.IsEnabled }),
            cancellationToken);

        return CreateResult(
            rawArguments,
            new { totalCount, items },
            items.Count,
            totalCount > items.Count);
    }
}
