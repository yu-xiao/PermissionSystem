using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class SsoUserBinding : BaseEntity
{
    public Guid ProviderId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string ExternalUserId { get; set; } = string.Empty;

    public string? ExternalUserName { get; set; }

    public string? ExternalEmail { get; set; }

    public string? ExternalPhone { get; set; }

    public Guid LocalUserId { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public string? ClaimsJson { get; set; }

    public SsoProvider? Provider { get; set; }

    public User? LocalUser { get; set; }
}
