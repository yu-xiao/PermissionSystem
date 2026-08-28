namespace PermissionSystem.Application.Mcp;

public static class McpBuiltInDatasetCatalog
{
    public static IReadOnlyList<McpDatasetTemplate> Datasets { get; } =
    [
        new(
            McpDatasetCodes.PlatformCapabilities,
            "Platform capabilities",
            "1.0",
            "Non-sensitive metadata describing enabled PermissionSystem capability families.",
            "Public",
            McpDatasetCodes.PlatformCapabilities,
            20,
            [
                new("code", "Code", "string", "Public", true, true),
                new("name", "Name", "string", "Public", true, true),
                new("status", "Status", "string", "Public", true, true)
            ]),
        new(
            McpDatasetCodes.DepartmentDirectory,
            "Department directory",
            "1.0",
            "Tenant-scoped department directory without internal identifiers or audit fields.",
            "Internal",
            McpDatasetCodes.DepartmentDirectory,
            100,
            [
                new("code", "Department code", "string", "Internal", true, true),
                new("name", "Department name", "string", "Internal", true, true),
                new("parentCode", "Parent department code", "string", "Internal", false, true),
                new("isEnabled", "Enabled", "boolean", "Internal", true, true)
            ])
    ];
}

public sealed record McpDatasetTemplate(
    string DatasetCode,
    string DatasetName,
    string Version,
    string Description,
    string DataClassification,
    string HandlerCode,
    int MaxRows,
    IReadOnlyList<McpDatasetFieldTemplate> Fields);

public sealed record McpDatasetFieldTemplate(
    string FieldCode,
    string DisplayName,
    string DataType,
    string DataClassification,
    bool IsFilterable,
    bool IsDefault);
