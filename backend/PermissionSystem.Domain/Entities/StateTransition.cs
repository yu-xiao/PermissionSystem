using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class StateTransition : BaseEntity
{
    public Guid MachineId { get; set; }

    public string FromState { get; set; } = string.Empty;

    public string ToState { get; set; } = string.Empty;

    public string ActionCode { get; set; } = string.Empty;

    public string ActionName { get; set; } = string.Empty;

    public string? RequiredPermission { get; set; }

    public string? ConditionJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int Sort { get; set; }
}
