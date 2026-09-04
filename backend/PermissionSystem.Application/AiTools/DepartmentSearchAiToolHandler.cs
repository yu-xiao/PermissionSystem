using PermissionSystem.Application.Departments;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiTools;

public sealed class DepartmentSearchAiToolHandler : AiReadOnlyToolHandlerBase<AiSearchArguments>
{
    private readonly IDepartmentService _departmentService;
    private readonly IAiToolConfiguration _configuration;

    public DepartmentSearchAiToolHandler(
        IDepartmentService departmentService,
        IAiToolConfiguration? configuration = null)
    {
        _departmentService = departmentService;
        _configuration = configuration ?? new DefaultAiToolConfiguration();
        Definition = new AiToolDefinition
        {
            ToolCode = "permission.departments.search",
            FunctionName = "search_departments",
            Version = "1.0",
            DisplayName = "Search departments",
            Description = "Search department summaries in the current tenant.",
            DataClassification = "Internal",
            DataScopePolicy = AiToolDataScopePolicies.CurrentTenant,
            RequiredPermissions =
            [
                AiCenterConstants.ToolQueryPermission,
                AiCenterConstants.DepartmentQueryPermission,
                "system:department:view"
            ],
            TimeoutSeconds = 30,
            MaxRows = _configuration.MaxToolRows,
            InputSchemaJson = """{"type":"object","properties":{"keyword":{"type":"string","maxLength":100},"isEnabled":{"type":"boolean"},"limit":{"type":"integer","minimum":1,"maximum":200}},"additionalProperties":false}""",
            OutputSchemaJson = """{"type":"object","required":["totalCount","items"],"properties":{"totalCount":{"type":"integer","minimum":0},"items":{"type":"array","items":{"type":"object","required":["id","code","name","isEnabled"],"properties":{"id":{"type":"string","format":"uuid"},"parentId":{"type":["string","null"],"format":"uuid"},"code":{"type":"string"},"name":{"type":"string"},"isEnabled":{"type":"boolean"}},"additionalProperties":false}}},"additionalProperties":false}"""
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
        var departments = Flatten(
            await _departmentService.GetTreeAsync(context.TenantId, cancellationToken));
        IEnumerable<DepartmentTreeResponse> query = departments;
        if (!string.IsNullOrWhiteSpace(arguments.Keyword))
        {
            var keyword = NormalizeKeyword(arguments.Keyword);
            query = query.Where(item =>
                item.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
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

        return CreateResult(
            rawArguments,
            new { totalCount = matched.Count, items },
            items.Count,
            matched.Count > items.Count);
    }

    private static IReadOnlyList<DepartmentTreeResponse> Flatten(
        IReadOnlyList<DepartmentTreeResponse> roots)
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
}
