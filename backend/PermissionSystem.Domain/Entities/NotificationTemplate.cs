using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class NotificationTemplate : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string TitleTemplate { get; set; } = string.Empty;

    public string ContentTemplate { get; set; } = string.Empty;

    public string Status { get; set; } = "Enabled";

    public int Sort { get; set; }

    public string? Remark { get; set; }
}
