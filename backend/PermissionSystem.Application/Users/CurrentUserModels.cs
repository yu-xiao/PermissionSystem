using PermissionSystem.Application.Menus;

namespace PermissionSystem.Application.Users;

public sealed class CurrentUserResponse
{
    public Guid? UserId { get; init; }

    public Guid? TenantId { get; init; }

    public string? Username { get; init; }

    public bool IsSuperAdmin { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public IReadOnlyCollection<string> PermissionCodes { get; init; } = [];
}

public interface ICurrentUserAppService
{
    Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuTreeResponse>> GetCurrentUserMenusAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetCurrentUserPermissionCodesAsync(CancellationToken cancellationToken = default);
}
