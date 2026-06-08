using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class SsoRoleMapping : BaseEntity
{
    public Guid ProviderId { get; set; }

    public string ExternalRole { get; set; } = string.Empty;

    public Guid LocalRoleId { get; set; }

    public SsoProvider? Provider { get; set; }

    public Role? LocalRole { get; set; }
}
