using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class DictionaryType : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Status { get; set; } = "Enabled";

    public int Sort { get; set; }
}
