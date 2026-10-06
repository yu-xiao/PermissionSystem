using PermissionSystem.Application.Permissions;

namespace PermissionSystem.Application.DataPermissions;

public interface IDataScopeResolver
{
    Task<DataScopeContext> ResolveAsync(PermissionSubject subject, CancellationToken cancellationToken = default);
}
