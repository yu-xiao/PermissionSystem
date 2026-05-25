namespace PermissionSystem.Application.Users;

public sealed class MyProfileResponse
{
    public Guid Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string NickName { get; init; } = string.Empty;

    public string RealName { get; init; } = string.Empty;

    public string? Avatar { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public Guid? DepartmentId { get; init; }

    public string? DepartmentName { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public IReadOnlyCollection<string> Permissions { get; init; } = [];

    public Guid TenantId { get; init; }

    public string? TenantName { get; init; }

    public DateTimeOffset? LastLoginTime { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class UpdateMyProfileRequest
{
    public string? NickName { get; init; }

    public string? RealName { get; init; }

    public string? Avatar { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }
}

public sealed class ChangeMyPasswordRequest
{
    public string OldPassword { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class LogoutMySessionRequest
{
    public string? RefreshToken { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? TraceId { get; init; }
}

public interface IMeService
{
    Task<MyProfileResponse> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<MyProfileResponse> UpdateProfileAsync(UpdateMyProfileRequest request, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(ChangeMyPasswordRequest request, CancellationToken cancellationToken = default);

    Task LogoutAsync(LogoutMySessionRequest request, CancellationToken cancellationToken = default);

    Task LogoutAllAsync(LogoutMySessionRequest request, CancellationToken cancellationToken = default);
}
