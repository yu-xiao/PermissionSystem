using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class McpClientDatasetGrant : BaseEntity
{
    public Guid ClientBindingId { get; set; }

    public Guid DatasetId { get; set; }

    public string AllowedFieldsJson { get; set; } = "[]";

    public string ApprovedSchemaHash { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
