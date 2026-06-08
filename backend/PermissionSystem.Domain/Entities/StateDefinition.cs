using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class StateDefinition : BaseEntity
{
    public Guid MachineId { get; set; }

    public string StateCode { get; set; } = string.Empty;

    public string StateName { get; set; } = string.Empty;

    public string StateType { get; set; } = "Normal";

    public string? Color { get; set; }

    public int Sort { get; set; }

    public bool IsInitial { get; set; }

    public bool IsFinal { get; set; }
}
