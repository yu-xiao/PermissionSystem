using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiTools;

public sealed class PermissionDiagnosticAiToolHandler : AiReadOnlyToolHandlerBase<PermissionDiagnosticRequest>
{
    public const string ToolCode = "permission.diagnose";
    public const string FunctionName = "diagnose_permission";
    private readonly IPermissionDiagnosticService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantContext _tenant;

    public PermissionDiagnosticAiToolHandler(IPermissionDiagnosticService service,
        ICurrentUserService currentUser, ITenantContext tenant)
    {
        _service = service;
        _currentUser = currentUser;
        _tenant = tenant;
    }

    public override AiToolDefinition Definition { get; } = new()
    {
        ToolCode = ToolCode,
        FunctionName = FunctionName,
        Version = "1.0",
        DisplayName = "权限排障",
        Description = "Diagnose one explicit menu ID, permission requirement, or effective data scope. Defaults to self. " +
            "Other users require server authorization and visibility. Ask for clarification for ambiguous IDs. " +
            "Use the server conclusion and limitations; do not infer business row visibility or change permissions.",
        RequiredPermissions = [AiCenterConstants.ToolQueryPermission],
        DataClassification = "Confidential",
        DataScopePolicy = AiToolDataScopePolicies.CurrentUserDataScope,
        TimeoutSeconds = 30,
        MaxRows = 1,
        InputSchemaJson = """{"type":"object","required":["kind"],"properties":{"kind":{"type":"string","enum":["Menu","Permission","DataScope"]},"targetUserId":{"type":"string","format":"uuid"},"menuId":{"type":"string","format":"uuid"},"permissionCode":{"type":"string","maxLength":500}},"additionalProperties":false}""",
        OutputSchemaJson = """{"type":"object","required":["version","target","evaluationBasis","evaluatedAt","conclusion","summary","checks","limitations","suggestedEntries","isTruncated"],"properties":{"version":{"const":1},"target":{"type":"object","required":["userId","kind"],"properties":{"userId":{"type":"string","format":"uuid"},"kind":{"type":"string","enum":["Menu","Permission","DataScope"]},"menuId":{"type":["string","null"]},"permissionCode":{"type":["string","null"]}},"additionalProperties":false},"evaluationBasis":{"type":"string","enum":["CurrentServerIdentity","CurrentConfiguration"]},"evaluatedAt":{"type":"string","format":"date-time"},"conclusion":{"type":"string","enum":["Allowed","Denied","Limited","InsufficientEvidence"]},"summary":{"type":"string"},"checks":{"type":"array","maxItems":20,"items":{"type":"object","required":["code","status","description","source"],"properties":{"code":{"type":"string"},"status":{"type":"string","enum":["Passed","Failed","NotEvaluated"]},"description":{"type":"string"},"source":{"type":"string"}},"additionalProperties":false}},"limitations":{"type":"array","items":{"type":"string"}},"suggestedEntries":{"type":"array","items":{"type":"object","required":["code","label"],"properties":{"code":{"type":"string"},"label":{"type":"string"}},"additionalProperties":false}},"isTruncated":{"type":"boolean"}},"additionalProperties":false}"""
    };

    protected override async Task<AiToolExecutionResult> ExecuteCoreAsync(AiToolExecutionContext context,
        PermissionDiagnosticRequest arguments, string rawArguments, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || context.ActorUserId != _currentUser.UserId ||
            context.TenantId != _currentUser.TenantId || context.TenantId != _tenant.TenantId)
            throw new BusinessException(ErrorCode.Forbidden, "The diagnostic execution context is invalid.");
        var diagnostic = await _service.DiagnoseAsync(arguments, cancellationToken);
        return CreateResult(rawArguments, diagnostic, 1, diagnostic.IsTruncated, permissionDiagnostic: diagnostic);
    }
}
