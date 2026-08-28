namespace PermissionSystem.Shared.Constants;

public static class AiCenterConstants
{
    public const string ApiResource = "permission-system-api";
    public const string McpResource = "permission-system-mcp";
    public const string McpScope = "permission-system-mcp";
    public const string McpDatasetQueryPermission = "mcp:dataset:query";
    public const string McpIntrospectionClientId = "permission-system-mcp-server";
    public const string McpClientViewPermission = "ai:mcp-client:view";
    public const string McpClientManagePermission = "ai:mcp-client:manage";
    public const string McpClientSecretPermission = "ai:mcp-client:secret";
    public const string McpAuditViewPermission = "ai:mcp-audit:view";
    public const string McpClientCreateOperationCode = "ai:mcp-client:create";
    public const string McpClientUpdateOperationCode = "ai:mcp-client:update";
    public const string McpClientSecretOperationCode = "ai:mcp-client:secret";
    public const string McpClientStatusOperationCode = "ai:mcp-client:status";
    public const string ChatUsePermission = "ai:chat:use";

    public const string DocumentDraftPermission = "ai:document:draft";
    public const string DocumentExecutePermission = "ai:document:execute";
    public const string DocumentExecuteOperationCode = "ai:document:execute";
    public const string ConversationViewPermission = "ai:conversation:view";
    public const string ToolQueryPermission = "ai:tool:query";
    public const string ProviderViewPermission = "ai:provider:view";
    public const string ProviderCreatePermission = "ai:provider:create";
    public const string ProviderUpdatePermission = "ai:provider:update";
    public const string ProviderDeletePermission = "ai:provider:delete";
    public const string ProviderTestPermission = "ai:provider:test";
    public const string ProviderCompliancePermission = "ai:provider:compliance";
    public const string UserQueryPermission = "ai:tool:user-query";
    public const string DepartmentQueryPermission = "ai:tool:department-query";
    public const string RoleQueryPermission = "ai:tool:role-query";
    public const string LoginLogQueryPermission = "ai:tool:login-log-query";
    public const string OperationLogQueryPermission = "ai:tool:operation-log-query";
    public const string ReportDatasetQueryPermission = "ai:tool:dataset-query";
}
