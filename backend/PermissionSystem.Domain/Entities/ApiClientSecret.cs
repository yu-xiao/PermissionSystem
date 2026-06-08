using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class ApiClientSecret : BaseEntity
{
    public Guid ClientId { get; set; }

    public string SecretHash { get; set; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}
