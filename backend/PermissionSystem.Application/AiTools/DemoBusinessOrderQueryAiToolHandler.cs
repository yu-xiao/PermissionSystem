using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiTools;

public sealed class DemoBusinessOrderQueryAiToolHandler(
    IDemoBusinessOrderReadOnlyQueryService queries, IAiQueryAccessGuard access, IAiToolConfiguration configuration)
    : AiReadOnlyToolHandlerBase<DemoBusinessOrderReadOnlyQuery>
{
    public const string ToolCode = "business.demo_business_order.query";
    public override bool IsEnabled => configuration.EnableDemoBusinessOrderQueryTool;
    public override AiToolDefinition Definition { get; } = new()
    {
        ToolCode = ToolCode, FunctionName = "query_demo_business_orders", Version = DemoBusinessOrderReadOnlyContract.Version,
        DisplayName = "Query Demo business orders",
        Description = "Read existing DemoBusinessOrder records within the current tenant and authorized scope. Returns safe details and the full matching count, unit orders. Keyword only matches order number or title. Approval status is current state. No customer, amount, time filters or historical states. Never use the draft tool for a query or guess departments. Query does not create, confirm or change an order.",
        DataClassification = "Confidential", DataScopePolicy = AiToolDataScopePolicies.CurrentUserDataScope,
        RequiredPermissions = [AiCenterConstants.ToolQueryPermission, DemoBusinessOrderReadOnlyContract.ViewPermission],
        TimeoutSeconds = 30, MaxRows = Math.Min(configuration.MaxToolRows, 200),
        InputSchemaJson = """{"type":"object","properties":{"keyword":{"type":"string","maxLength":100},"approvalStatus":{"type":"string","enum":["Draft","Pending","Approved","Rejected","Withdrawn","Cancelled"]},"departmentId":{"type":"string","format":"uuid"},"departmentScope":{"type":"string","enum":["Authorized","CurrentDepartment"]},"limit":{"type":"integer","minimum":1,"maximum":200}},"additionalProperties":false}""",
        OutputSchemaJson = """{"type":"object","required":["totalCount","displayedRowCount","items","isTruncated","evaluationBasis","limitations"],"properties":{"totalCount":{"type":"integer","minimum":0},"displayedRowCount":{"type":"integer","minimum":0},"items":{"type":"array","items":{"type":"object","required":["id","orderNo","title","approvalStatus","createdAt"],"properties":{"id":{"type":"string","format":"uuid"},"orderNo":{"type":"string"},"title":{"type":"string"},"approvalStatus":{"type":"string","enum":["Draft","Pending","Approved","Rejected","Withdrawn","Cancelled"]},"departmentId":{"type":["string","null"],"format":"uuid"},"createdAt":{"type":"string","format":"date-time"}},"additionalProperties":false}},"isTruncated":{"type":"boolean"},"evaluationBasis":{"type":"string"},"limitations":{"type":"array","items":{"type":"string"}}},"additionalProperties":false}"""
    };

    protected override async Task<AiToolExecutionResult> ExecuteCoreAsync(AiToolExecutionContext context,
        DemoBusinessOrderReadOnlyQuery arguments, string rawArguments, CancellationToken cancellationToken)
    {
        if (!IsEnabled) throw new BusinessException(ErrorCode.Forbidden, "Demo query tool is disabled.");
        var actor = await access.AuthorizeAsync(ToolCode, cancellationToken);
        if (actor.UserId != context.ActorUserId || actor.TenantId != context.TenantId)
            throw new BusinessException(ErrorCode.Forbidden, "Invalid Demo query execution context.");
        var effective = DemoBusinessOrderReadOnlyContract.Normalize(arguments, configuration.MaxToolRows);
        var output = await queries.QueryAsync(effective, cancellationToken);
        var latestActor = await access.AuthorizeAsync(ToolCode, cancellationToken);
        var latestScope = await access.GetUserScopeAsync(latestActor, cancellationToken);
        if (!IsEnabled || actor.UserId != latestActor.UserId || actor.TenantId != latestActor.TenantId ||
            AiStructuredResults.ScopeFingerprint(output.Scope) != AiStructuredResults.ScopeFingerprint(latestScope))
            throw new BusinessException(ErrorCode.Forbidden, "Demo query access changed; please retry.");
        var truncated = output.Data.TotalCount > output.Data.DisplayedRowCount;
        return CreateResult(rawArguments, new
        {
            output.Data.TotalCount, output.Data.DisplayedRowCount, output.Data.Items, isTruncated = truncated,
            evaluationBasis = DemoBusinessOrderReadOnlyContract.EvaluationBasis,
            limitations = new[] { DemoBusinessOrderReadOnlyContract.Limitation }
        }, output.Data.DisplayedRowCount, truncated, DemoBusinessOrderReadOnlyContract.DatasetCode,
            DemoBusinessOrderReadOnlyContract.Version, queryContext: new()
            {
                Parameters = AiStructuredResults.Parameters(output.EffectiveQuery),
                DataScopeFingerprint = AiStructuredResults.ScopeFingerprint(output.Scope)
            }, evaluationBasis: DemoBusinessOrderReadOnlyContract.EvaluationBasis, demoOrders: output.Data,
            queriedAtOverride: output.QueriedAt);
    }
}
