namespace PermissionSystem.Application.Menus;

public sealed class CreateMenuRequest
{
    public Guid TenantId { get; init; }

    public Guid? ParentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Path { get; init; }

    public string? Component { get; init; }

    public string? Redirect { get; init; }

    public string? Icon { get; init; }

    public int Sort { get; init; }

    public bool Visible { get; init; } = true;

    public bool KeepAlive { get; init; }

    public string MenuType { get; init; } = "Menu";

    public string? PermissionCode { get; init; }
}

public sealed class UpdateMenuRequest
{
    public Guid? ParentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Path { get; init; }

    public string? Component { get; init; }

    public string? Redirect { get; init; }

    public string? Icon { get; init; }

    public int Sort { get; init; }

    public bool Visible { get; init; } = true;

    public bool KeepAlive { get; init; }

    public string MenuType { get; init; } = "Menu";

    public string? PermissionCode { get; init; }
}

public sealed class MenuTreeResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid? ParentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Path { get; init; }

    public string? Component { get; init; }

    public string? Redirect { get; init; }

    public string? Icon { get; init; }

    public int Sort { get; init; }

    public bool Visible { get; init; }

    public bool KeepAlive { get; init; }

    public string MenuType { get; init; } = string.Empty;

    public string? PermissionCode { get; init; }

    public IReadOnlyList<MenuTreeResponse> Children { get; init; } = [];
}

public interface IMenuService
{
    Task<IReadOnlyList<MenuTreeResponse>> GetTreeAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);

    Task<MenuTreeResponse> CreateAsync(CreateMenuRequest request, CancellationToken cancellationToken = default);

    Task<MenuTreeResponse> UpdateAsync(Guid id, UpdateMenuRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
