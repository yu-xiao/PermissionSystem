using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class SystemConfig : BaseEntity
{
    public string ConfigKey { get; set; } = string.Empty;

    public string ConfigValue { get; set; } = string.Empty;

    public string ConfigType { get; set; } = "String";

    public string GroupCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsEncrypted { get; set; }

    public bool IsSystem { get; set; }

    public string Status { get; set; } = "Enabled";

    public int Sort { get; set; }
}
