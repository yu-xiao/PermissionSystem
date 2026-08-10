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
    public byte[]? ConcurrencyToken { get; init; }

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

public sealed class RoleUsersQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }
}

public sealed class SaveRoleUsersRequest
{
    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}

public sealed class RoleResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; }

    public bool IsBuiltin { get; init; }

    public bool IsSuperAdminRole { get; init; }

    public int Sort { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class RoleUserResponse
{
    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string NickName { get; init; } = string.Empty;

    public string RealName { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string? DepartmentName { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool Checked { get; init; }
}

public sealed class RoleUsersResponse
{
    public IReadOnlyCollection<Guid> SelectedUserIds { get; init; } = [];

    public PagedResult<RoleUserResponse> Users { get; init; } =
        PagedResult<RoleUserResponse>.Create(Array.Empty<RoleUserResponse>(), 1, 10, 0);
}

public interface IRoleService
{
    Task<PagedResult<RoleResponse>> GetPagedAsync(RoleQueryRequest request, CancellationToken cancellationToken = default);

    Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    Task<RoleResponse> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task AssignMenusAsync(Guid id, AssignRoleMenusRequest request, CancellationToken cancellationToken = default);

    Task AssignPermissionsAsync(Guid id, AssignRolePermissionsRequest request, CancellationToken cancellationToken = default);

    Task<RoleUsersResponse> GetRoleUsersAsync(
        Guid roleId,
        RoleUsersQueryRequest request,
        CancellationToken cancellationToken = default);

    Task SaveRoleUsersAsync(
        Guid roleId,
        SaveRoleUsersRequest request,
        CancellationToken cancellationToken = default);

    Task<RolePermissionMatrixResponse> GetPermissionMatrixAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task SavePermissionMatrixAsync(
        Guid roleId,
        SaveRolePermissionMatrixRequest request,
        CancellationToken cancellationToken = default);
}
