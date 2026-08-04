namespace PermissionSystem.Application.Abstractions;

public interface ITenantContext
{
    Guid? TenantId { get; }

    string? Source { get; }

    bool IsResolved { get; }

    bool IsSuperAdmin { get; }

    bool IsSystemScopeActive { get; }

    bool IsHttpRequest { get; }

    void SetTenant(Guid tenantId, string source);

    void MarkAsSuperAdmin(bool isSuperAdmin);

    void MarkAsHttpRequest();
}
