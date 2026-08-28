using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class McpDatasetField : BaseEntity
{
    public Guid DatasetId { get; set; }

    public string FieldCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public string DataClassification { get; set; } = string.Empty;

    public bool IsFilterable { get; set; }

    public bool IsDefault { get; set; } = true;
}
