using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Application.Tenants;

public sealed class TenantContext : ITenantContext
{
    private int _systemScopeDepth;

    public Guid? TenantId { get; private set; }

    public string? Source { get; private set; }

    public bool IsResolved => TenantId.HasValue;

    public bool IsSuperAdmin { get; private set; }

    public bool IsSystemScopeActive => _systemScopeDepth > 0;

    public bool IsHttpRequest { get; private set; }

    public void SetTenant(Guid tenantId, string source)
    {
        TenantId = tenantId;
        Source = source;
    }

    public void MarkAsSuperAdmin(bool isSuperAdmin)
    {
        IsSuperAdmin = isSuperAdmin;
    }

    public void MarkAsHttpRequest()
    {
        IsHttpRequest = true;
    }

    internal void EnterSystemScope()
    {
        _systemScopeDepth++;
    }

    internal void ExitSystemScope()
    {
        if (_systemScopeDepth <= 0)
        {
            throw new InvalidOperationException("No active system tenant scope can be exited.");
        }

        _systemScopeDepth--;
    }
}
