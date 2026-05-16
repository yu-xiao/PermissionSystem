using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Users;

public sealed class UserQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreateUserRequest
{
    public Guid TenantId { get; init; }

    public Guid? DepartmentId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public sealed class UpdateUserRequest
{
    public Guid? DepartmentId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public sealed class SetUserEnabledRequest
{
    public bool IsEnabled { get; init; }
}

public sealed class ResetUserPasswordRequest
{
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class AssignUserRolesRequest
{
    public IReadOnlyCollection<Guid> RoleIds { get; init; } = [];
}

public sealed class UserResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid? DepartmentId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public bool IsEnabled { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public IReadOnlyCollection<Guid> RoleIds { get; init; } = [];
}

public interface IUserService
{
    Task<PagedResult<UserResponse>> GetPagedAsync(UserQueryRequest request, CancellationToken cancellationToken = default);

    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(Guid id, SetUserEnabledRequest request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(Guid id, ResetUserPasswordRequest request, CancellationToken cancellationToken = default);

    Task AssignRolesAsync(Guid id, AssignUserRolesRequest request, CancellationToken cancellationToken = default);
}
