namespace PermissionSystem.Shared.Constants;

public static class AiCenterConstants
{
    public const string ApiResource = "permission-system-api";
    public const string McpResource = "permission-system-mcp";
    public const string McpScope = "permission-system-mcp";
    public const string McpDatasetQueryPermission = "mcp:dataset:query";
    public const string McpIntrospectionClientId = "permission-system-mcp-server";
    public const string ChatUsePermission = "ai:chat:use";

    public const string DocumentDraftPermission = "ai:document:draft";
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
