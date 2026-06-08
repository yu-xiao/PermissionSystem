using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class IpAccessRule : BaseEntity
{
    public string RuleType { get; set; } = "Blacklist";

    public string IpPattern { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;
}
