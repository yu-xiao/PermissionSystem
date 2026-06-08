using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class StateTransitionLog : BaseEntity
{
    public string BusinessType { get; set; } = string.Empty;

    public string BusinessId { get; set; } = string.Empty;

    public string FromState { get; set; } = string.Empty;

    public string ToState { get; set; } = string.Empty;

    public string ActionCode { get; set; } = string.Empty;

    public string ActionName { get; set; } = string.Empty;

    public Guid? OperatorUserId { get; set; }

    public string? OperatorUserName { get; set; }

    public string? Comment { get; set; }
}
