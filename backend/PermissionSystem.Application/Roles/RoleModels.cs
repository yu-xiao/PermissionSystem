using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Roles;

public sealed class RoleQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreateRoleRequest
{
    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; } = true;

    public int Sort { get; init; }
}

public sealed class UpdateRoleRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; } = true;

    public int Sort { get; init; }
}

public sealed class AssignRoleMenusRequest
{
    public IReadOnlyCollection<Guid> MenuIds { get; init; } = [];
}

public sealed class AssignRolePermissionsRequest
{
    public IReadOnlyCollection<Guid> PermissionIds { get; init; } = [];
}

public sealed class RoleResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; }

    public int Sort { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public interface IRoleService
{
    Task<PagedResult<RoleResponse>> GetPagedAsync(RoleQueryRequest request, CancellationToken cancellationToken = default);

    Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    Task<RoleResponse> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task AssignMenusAsync(Guid id, AssignRoleMenusRequest request, CancellationToken cancellationToken = default);

    Task AssignPermissionsAsync(Guid id, AssignRolePermissionsRequest request, CancellationToken cancellationToken = default);
}
