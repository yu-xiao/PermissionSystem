using PermissionSystem.Application.Menus;
using PermissionSystem.Application.Permissions;

namespace PermissionSystem.Application.Users;

public interface IUserMenuResolver
{
    Task<IReadOnlyList<MenuTreeResponse>> ResolveMenusAsync(
        PermissionSubject subject, CancellationToken cancellationToken = default);
}
