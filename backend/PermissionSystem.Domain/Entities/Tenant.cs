using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.Initializing;

    public string? InitializationStep { get; set; }

    public int InitializationProgress { get; set; }

    public int InitializationAttempts { get; set; }

    public string? InitializationJobId { get; set; }

    public string? InitializationError { get; set; }

    public DateTimeOffset? InitializationStartedAt { get; set; }

    public DateTimeOffset? InitializedAt { get; set; }

    public DateTimeOffset StatusChangedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<Department> Departments { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];

    public ICollection<Role> Roles { get; set; } = [];
}
