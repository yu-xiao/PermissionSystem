using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.LoginLogs;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Users;

public sealed class MeService : IMeService
{
    private const int MinimumPasswordLength = 8;

    private readonly ICurrentUserService _currentUserService;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<LoginLog> _loginLogRepository;
    private readonly ILoginLogService _loginLogService;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly IUnitOfWork _unitOfWork;

    public MeService(
        ICurrentUserService currentUserService,
        IRepository<User> userRepository,
        IRepository<Department> departmentRepository,
        IRepository<Tenant> tenantRepository,
        IRepository<Role> roleRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<LoginLog> loginLogRepository,
        ILoginLogService loginLogService,
        IPasswordHashService passwordHashService,
        IUserSessionService userSessionService,
        ITokenRevocationService tokenRevocationService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _tenantRepository = tenantRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _loginLogRepository = loginLogRepository;
        _loginLogService = loginLogService;
        _passwordHashService = passwordHashService;
        _userSessionService = userSessionService;
        _tokenRevocationService = tokenRevocationService;
        _unitOfWork = unitOfWork;
    }

    public Task<MyProfileResponse> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var user = GetCurrentUserOrThrow();
        return Task.FromResult(ToProfileResponse(user));
    }

    public async Task<MyProfileResponse> UpdateProfileAsync(
        UpdateMyProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = GetCurrentUserOrThrow();
        var displayName = FirstNotBlank(request.NickName, request.RealName);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName;
        }

        user.AvatarUrl = NormalizeNullable(request.Avatar);
        user.Email = NormalizeNullable(request.Email);
        user.PhoneNumber = NormalizeNullable(request.PhoneNumber);

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToProfileResponse(user);
    }

    public async Task ChangePasswordAsync(
        ChangeMyPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = GetCurrentUserOrThrow();

        ValidateRequired(request.OldPassword, "Old password is required.");
        ValidateRequired(request.NewPassword, "New password is required.");
        ValidateRequired(request.ConfirmPassword, "Confirm password is required.");

        if (!_passwordHashService.VerifyPassword(user.PasswordHash, request.OldPassword))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Old password is incorrect.");
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "New password and confirm password do not match.");
        }

        if (_passwordHashService.VerifyPassword(user.PasswordHash, request.NewPassword))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "New password cannot be the same as old password.");
        }

        ValidatePasswordPolicy(request.NewPassword);

        user.PasswordHash = _passwordHashService.HashPassword(request.NewPassword);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _tokenRevocationService.RevokeUserRefreshTokensAsync(user.Id, cancellationToken);
        await _userSessionService.RevokeUserSessionsAsync(user.Id, "Password changed.", cancellationToken);
    }

    public async Task LogoutAsync(
        LogoutMySessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_currentUserService.SessionId))
        {
            await _userSessionService.RevokeAsync(_currentUserService.SessionId, "Logout.", cancellationToken);
        }

        await _tokenRevocationService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        await WriteLogoutLogAsync(request, cancellationToken);
    }

    public async Task LogoutAllAsync(
        LogoutMySessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "Current user is not authenticated.");

        await _tokenRevocationService.RevokeUserRefreshTokensAsync(userId, cancellationToken);
        await _userSessionService.RevokeUserSessionsAsync(userId, "Logout all devices.", cancellationToken);
        await WriteLogoutLogAsync(request, cancellationToken);
    }

    private User GetCurrentUserOrThrow()
    {
        var userId = _currentUserService.UserId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "Current user is not authenticated.");
        var tenantId = _currentUserService.TenantId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "Current tenant is not available.");

        return _userRepository.Query()
            .FirstOrDefault(entity => entity.Id == userId && entity.TenantId == tenantId)
            ?? throw new BusinessException(ErrorCode.NotFound, "Current user was not found.");
    }

    private MyProfileResponse ToProfileResponse(User user)
    {
        var roleIds = _userRoleRepository.Query()
            .Where(entity => entity.TenantId == user.TenantId && entity.UserId == user.Id)
            .Select(entity => entity.RoleId)
            .ToArray();

        var roleCodes = _roleRepository.Query()
            .Where(entity => entity.TenantId == user.TenantId && roleIds.Contains(entity.Id) && entity.IsEnabled)
            .OrderBy(entity => entity.Sort)
            .Select(entity => entity.Code)
            .ToArray();

        var departmentName = user.DepartmentId.HasValue
            ? _departmentRepository.Query()
                .Where(entity => entity.TenantId == user.TenantId && entity.Id == user.DepartmentId.Value)
                .Select(entity => entity.Name)
                .FirstOrDefault()
            : null;

        var tenantName = _tenantRepository.Query()
            .Where(entity => entity.Id == user.TenantId)
            .Select(entity => entity.Name)
            .FirstOrDefault();

        var lastLoginTime = user.LastLoginAt ?? _loginLogRepository.Query()
            .Where(entity =>
                entity.TenantId == user.TenantId &&
                entity.UserId == user.Id &&
                entity.LoginType == "password" &&
                entity.LoginResult == "Succeeded")
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(entity => (DateTimeOffset?)entity.CreatedAt)
            .FirstOrDefault();

        return new MyProfileResponse
        {
            Id = user.Id,
            UserName = user.UserName,
            NickName = user.DisplayName,
            RealName = user.DisplayName,
            Avatar = user.AvatarUrl,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            DepartmentId = user.DepartmentId,
            DepartmentName = departmentName,
            Roles = roleCodes,
            Permissions = _currentUserService.PermissionCodes,
            TenantId = user.TenantId,
            TenantName = tenantName,
            LastLoginTime = lastLoginTime,
            CreatedAt = user.CreatedAt
        };
    }

    private async Task WriteLogoutLogAsync(
        LogoutMySessionRequest request,
        CancellationToken cancellationToken)
    {
        await _loginLogService.CreateAsync(new CreateLoginLogRequest
        {
            TenantId = _currentUserService.TenantId ?? Guid.Empty,
            UserId = _currentUserService.UserId,
            UserName = _currentUserService.Username ?? "unknown",
            LoginType = "Logout",
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            LoginResult = "Succeeded",
            TraceId = request.TraceId
        }, cancellationToken);
    }

    private static void ValidatePasswordPolicy(string password)
    {
        if (password.Length < MinimumPasswordLength ||
            !password.Any(char.IsLetter) ||
            !password.Any(char.IsDigit))
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "New password must be at least 8 characters and contain both letters and numbers.");
        }
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }

    private static string? FirstNotBlank(params string?[] values)
    {
        return values
            .Select(NormalizeNullable)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
