using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class DictionaryItem : BaseEntity
{
    public string TypeCode { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Color { get; set; }

    public string? CssClass { get; set; }

    public bool IsDefault { get; set; }

    public string Status { get; set; } = "Enabled";

    public int Sort { get; set; }

    public string? Remark { get; set; }
}
