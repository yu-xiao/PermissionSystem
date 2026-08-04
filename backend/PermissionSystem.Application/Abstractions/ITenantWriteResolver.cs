namespace PermissionSystem.Application.Abstractions;

public interface ITenantWriteResolver
{
    Guid ResolveTenantId(Guid? requestedTenantId = null);
}
