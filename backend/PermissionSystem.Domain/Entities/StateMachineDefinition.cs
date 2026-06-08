using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class StateMachineDefinition : BaseEntity
{
    public string BusinessType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;
}
