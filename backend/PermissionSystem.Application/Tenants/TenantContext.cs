using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Application.Tenants;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public string? Source { get; private set; }

    public bool IsResolved => TenantId.HasValue;

    public bool IsSuperAdmin { get; private set; }

    public bool IsTenantFilterDisabled { get; private set; }

    public void SetTenant(Guid tenantId, string source)
    {
        TenantId = tenantId;
        Source = source;
    }

    public void MarkAsSuperAdmin(bool isSuperAdmin)
    {
        IsSuperAdmin = isSuperAdmin;
    }

    public void DisableTenantFilter()
    {
        IsTenantFilterDisabled = true;
    }
}
