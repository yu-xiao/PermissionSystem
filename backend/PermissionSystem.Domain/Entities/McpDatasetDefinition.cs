using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class McpDatasetDefinition : BaseEntity
{
    public string DatasetCode { get; set; } = string.Empty;

    public string DatasetName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string DataClassification { get; set; } = string.Empty;

    public string HandlerCode { get; set; } = string.Empty;

    public int MaxRows { get; set; } = 100;

    public bool IsEnabled { get; set; } = true;
}
