using PermissionSystem.Application.Excels;
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
    public byte[]? ConcurrencyToken { get; init; }

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

    public bool IsBuiltin { get; init; }

    public bool IsSuperAdmin { get; init; }

    public bool IsCurrentUser { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];

    public IReadOnlyCollection<Guid> RoleIds { get; init; } = [];

    public IReadOnlyCollection<string> RoleCodes { get; init; } = [];
}

public sealed class UserExportRow
{
    [ExcelColumn("Username", Order = 1)]
    public string UserName { get; set; } = string.Empty;

    [ExcelColumn("Display Name", Order = 2)]
    public string DisplayName { get; set; } = string.Empty;

    [ExcelColumn("Email", Order = 3)]
    public string? Email { get; set; }

    [ExcelColumn("Phone Number", Order = 4)]
    public string? PhoneNumber { get; set; }

    [ExcelColumn("Enabled", Order = 5)]
    public bool IsEnabled { get; set; }

    [ExcelColumn("Created At", Order = 6)]
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class UserImportRow
{
    [ExcelColumn("Username", Order = 1, Required = true)]
    public string UserName { get; set; } = string.Empty;

    [ExcelColumn("Display Name", Order = 2, Required = true)]
    public string DisplayName { get; set; } = string.Empty;

    [ExcelColumn("Password", Order = 3, Required = true)]
    public string Password { get; set; } = string.Empty;

    [ExcelColumn("Email", Order = 4)]
    public string? Email { get; set; }

    [ExcelColumn("Phone Number", Order = 5)]
    public string? PhoneNumber { get; set; }

    [ExcelColumn("Enabled", Order = 6)]
    public bool IsEnabled { get; set; } = true;
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

    Task<byte[]> ExportAsync(UserQueryRequest request, CancellationToken cancellationToken = default);

    Task<byte[]> CreateImportTemplateAsync(CancellationToken cancellationToken = default);

    Task<ImportResult<UserImportRow>> ImportPreviewAsync(Stream stream, CancellationToken cancellationToken = default);
}
