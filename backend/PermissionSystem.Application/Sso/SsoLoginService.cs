using System.Security.Cryptography;
using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Sso;

public sealed class SsoLoginService : ISsoLoginService
{
    private static readonly TimeSpan LoginCodeTtl = TimeSpan.FromMinutes(3);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<RolePermission> _rolePermissionRepository;
    private readonly IRepository<Permission> _permissionRepository;
    private readonly IRepository<SsoUserBinding> _bindingRepository;
    private readonly IRepository<SsoRoleMapping> _roleMappingRepository;
    private readonly IRepository<SsoDepartmentMapping> _departmentMappingRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IRepository<SsoLoginLog> _loginLogRepository;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly ISsoConfiguration _ssoConfiguration;

    public SsoLoginService(
        IRepository<Tenant> tenantRepository,
        IRepository<User> userRepository,
        IRepository<Role> roleRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<RolePermission> rolePermissionRepository,
        IRepository<Permission> permissionRepository,
        IRepository<SsoUserBinding> bindingRepository,
        IRepository<SsoRoleMapping> roleMappingRepository,
        IRepository<SsoDepartmentMapping> departmentMappingRepository,
        IRepository<Department> departmentRepository,
        IRepository<SsoLoginLog> loginLogRepository,
        IPasswordHashService passwordHashService,
        ICacheService cacheService,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        ISsoConfiguration? ssoConfiguration = null)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _permissionRepository = permissionRepository;
        _bindingRepository = bindingRepository;
        _roleMappingRepository = roleMappingRepository;
        _departmentMappingRepository = departmentMappingRepository;
        _departmentRepository = departmentRepository;
        _loginLogRepository = loginLogRepository;
        _passwordHashService = passwordHashService;
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _ssoConfiguration = ssoConfiguration ?? new DefaultSsoConfiguration();
    }

    public async Task<SsoLoginCodeResponse> CompleteLoginAsync(
        SsoProvider provider,
        ExternalSsoUser externalUser,
        SsoLoginContext context,
        CancellationToken cancellationToken = default)
    {
        EnsureOidcEnabled();
        _tenantContext.SetTenant(provider.TenantId, "Sso");
        try
        {
            var user = await ResolveLocalUserAsync(provider, externalUser, cancellationToken);
            if (!user.IsEnabled)
            {
                throw new BusinessException(ErrorCode.Forbidden, "Local user is disabled.");
            }

            await ApplySsoMappingsAsync(provider, externalUser, user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var authenticatedUser = BuildAuthenticatedUser(user);
            var loginCode = GenerateLoginCode();
            var expiresAt = DateTimeOffset.UtcNow.Add(LoginCodeTtl);
            await _cacheService.SetAsync(
                BuildLoginCodeCacheKey(loginCode),
                ToCacheEntry(authenticatedUser),
                LoginCodeTtl,
                cancellationToken: cancellationToken);

            user.LastLoginAt = DateTimeOffset.UtcNow;
            _userRepository.Update(user);
            await WriteLogAsync(provider, externalUser, user, SsoLoginResult.Success, null, context, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SsoLoginCodeResponse
            {
                LoginCode = loginCode,
                ExpiresAt = expiresAt,
                User = authenticatedUser
            };
        }
        catch (BusinessException exception)
        {
            await WriteLogAsync(
                provider,
                externalUser,
                null,
                ResolveLoginResult(exception.Message),
                exception.Message,
                context,
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
        catch (Exception exception)
        {
            await WriteLogAsync(provider, externalUser, null, SsoLoginResult.Failed, exception.Message, context, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<AuthenticatedUser?> ConsumeLoginCodeAsync(
        string loginCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(loginCode))
        {
            return null;
        }

        if (!_ssoConfiguration.Enabled || !_ssoConfiguration.EnableOidc)
        {
            await _cacheService.RemoveAsync(BuildLoginCodeCacheKey(loginCode.Trim()), cancellationToken);
            return null;
        }

        var cacheKey = BuildLoginCodeCacheKey(loginCode.Trim());
        var entry = await _cacheService.GetAsync<SsoLoginCodeCacheEntry>(cacheKey, cancellationToken);
        await _cacheService.RemoveAsync(cacheKey, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        return new AuthenticatedUser(
            entry.UserId,
            entry.Username,
            entry.TenantId,
            entry.DepartmentId,
            entry.SecurityStamp,
            entry.Roles,
            entry.PermissionCodes);
    }

    public async Task RecordFailureAsync(
        SsoProvider? provider,
        ExternalSsoUser? externalUser,
        string failureReason,
        SsoLoginContext context,
        CancellationToken cancellationToken = default)
    {
        await WriteLogAsync(
            provider,
            externalUser,
            null,
            SsoLoginResult.Failed,
            failureReason,
            context,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> ResolveLocalUserAsync(
        SsoProvider provider,
        ExternalSsoUser externalUser,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalUser.ExternalUserId))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "External user id is required.");
        }

        var tenant = _tenantRepository.QueryForTenant(provider.TenantId)
            .FirstOrDefault(entity => !entity.IsDeleted && entity.Id == provider.TenantId && entity.TenantId == provider.TenantId);
        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            throw new BusinessException(ErrorCode.Forbidden, "Tenant is disabled.");
        }

        var binding = _bindingRepository.QueryForTenant(provider.TenantId)
            .FirstOrDefault(entity =>
                !entity.IsDeleted &&
                entity.TenantId == provider.TenantId &&
                entity.ProviderId == provider.Id &&
                entity.ExternalUserId == externalUser.ExternalUserId);
        if (binding is not null)
        {
            var boundUser = GetUserOrThrow(binding.LocalUserId, provider.TenantId);
            UpdateBinding(binding, externalUser);
            return boundUser;
        }

        var user = provider.AutoBindUser
            ? FindMatchedUser(provider.TenantId, externalUser)
            : null;
        if (user is null)
        {
            if (!provider.AutoCreateUser || !_ssoConfiguration.AllowAutoCreateUser)
            {
                throw new BusinessException(ErrorCode.Forbidden, "External user is not bound to a local user.");
            }

            user = await CreateLocalUserAsync(provider, externalUser, cancellationToken);
        }

        await _bindingRepository.AddAsync(new SsoUserBinding
        {
            TenantId = provider.TenantId,
            ProviderId = provider.Id,
            ProviderCode = provider.ProviderCode,
            ExternalUserId = externalUser.ExternalUserId,
            ExternalUserName = NormalizeOptional(externalUser.UserName),
            ExternalEmail = NormalizeOptional(externalUser.Email),
            ExternalPhone = NormalizeOptional(externalUser.Phone),
            LocalUserId = user.Id,
            LastLoginAt = DateTimeOffset.UtcNow,
            ClaimsJson = JsonSerializer.Serialize(externalUser.Claims, JsonOptions)
        }, cancellationToken);

        return user;
    }

    private User GetUserOrThrow(Guid userId, Guid tenantId)
    {
        return _userRepository.QueryForTenant(tenantId)
            .FirstOrDefault(entity => !entity.IsDeleted && entity.TenantId == tenantId && entity.Id == userId)
            ?? throw new BusinessException(ErrorCode.NotFound, "Bound local user was not found.");
    }

    private User? FindMatchedUser(Guid tenantId, ExternalSsoUser externalUser)
    {
        var email = NormalizeOptional(externalUser.Email);
        if (email is not null)
        {
            var users = _userRepository.QueryForTenant(tenantId)
                .Where(entity => !entity.IsDeleted && entity.TenantId == tenantId && entity.Email == email)
                .Take(2)
                .ToList();
            if (users.Count > 1)
            {
                throw new BusinessException(ErrorCode.Conflict, "Multiple local users matched external email.");
            }

            if (users.Count == 1)
            {
                return users[0];
            }
        }

        var phone = NormalizeOptional(externalUser.Phone);
        if (phone is not null)
        {
            var users = _userRepository.QueryForTenant(tenantId)
                .Where(entity => !entity.IsDeleted && entity.TenantId == tenantId && entity.PhoneNumber == phone)
                .Take(2)
                .ToList();
            if (users.Count > 1)
            {
                throw new BusinessException(ErrorCode.Conflict, "Multiple local users matched external phone.");
            }

            if (users.Count == 1)
            {
                return users[0];
            }
        }

        var userName = NormalizeOptional(externalUser.UserName);
        if (userName is null)
        {
            return null;
        }

        var normalizedUserName = userName.ToUpperInvariant();
        return _userRepository.QueryForTenant(tenantId)
            .FirstOrDefault(entity =>
                !entity.IsDeleted &&
                entity.TenantId == tenantId &&
                entity.NormalizedUserName == normalizedUserName);
    }

    private async Task<User> CreateLocalUserAsync(
        SsoProvider provider,
        ExternalSsoUser externalUser,
        CancellationToken cancellationToken)
    {
        var userName = GenerateAvailableUserName(provider.TenantId, externalUser);
        var user = new User
        {
            TenantId = provider.TenantId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = NormalizeOptional(externalUser.Email),
            PhoneNumber = NormalizeOptional(externalUser.Phone),
            DisplayName = NormalizeOptional(externalUser.DisplayName) ?? userName,
            PasswordHash = _passwordHashService.HashPassword(GenerateRandomPassword()),
            IsEnabled = true,
            IsBuiltin = false
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user;
    }

    private async Task ApplySsoMappingsAsync(
        SsoProvider provider,
        ExternalSsoUser externalUser,
        User user,
        CancellationToken cancellationToken)
    {
        var authorizationChanged = false;
        var roleIds = ResolveSsoRoleIds(provider, externalUser);
        if (roleIds.Count > 0)
        {
            var existingRoleIds = _userRoleRepository.QueryForTenant(provider.TenantId)
                .Where(entity => !entity.IsDeleted && entity.TenantId == provider.TenantId && entity.UserId == user.Id)
                .Select(entity => entity.RoleId)
                .ToHashSet();
            foreach (var roleId in roleIds.Where(roleId => !existingRoleIds.Contains(roleId)))
            {
                await _userRoleRepository.AddAsync(new UserRole
                {
                    TenantId = provider.TenantId,
                    UserId = user.Id,
                    RoleId = roleId
                }, cancellationToken);
                authorizationChanged = true;
            }
        }

        var departmentId = ResolveSsoDepartmentId(provider, externalUser);
        if (departmentId.HasValue && user.DepartmentId != departmentId.Value)
        {
            user.DepartmentId = departmentId.Value;
            _userRepository.Update(user);
            authorizationChanged = true;
        }

        if (authorizationChanged)
        {
            user.RotateSecurityStamp();
        }
    }

    private IReadOnlyCollection<Guid> ResolveSsoRoleIds(SsoProvider provider, ExternalSsoUser externalUser)
    {
        var externalRoles = NormalizeExternalValues(externalUser.Roles);
        if (externalRoles.Count > 0)
        {
            var mappedRoleIds = _roleMappingRepository.QueryForTenant(provider.TenantId)
                .Where(entity =>
                    !entity.IsDeleted &&
                    entity.TenantId == provider.TenantId &&
                    entity.ProviderId == provider.Id)
                .ToList()
                .Where(entity => externalRoles.Contains(entity.ExternalRole))
                .Select(entity => entity.LocalRoleId)
                .Distinct()
                .ToArray();
            var roles = GetAssignableRoles(provider.TenantId, mappedRoleIds);
            if (roles.Count > 0)
            {
                return roles.Select(entity => entity.Id).ToArray();
            }
        }

        return ResolveDefaultRoleIds(provider);
    }

    private Guid? ResolveSsoDepartmentId(SsoProvider provider, ExternalSsoUser externalUser)
    {
        var externalDepartments = NormalizeExternalValues(externalUser.Departments);
        if (externalDepartments.Count == 0)
        {
            return null;
        }

        var mappedDepartmentIds = _departmentMappingRepository.QueryForTenant(provider.TenantId)
            .Where(entity =>
                !entity.IsDeleted &&
                entity.TenantId == provider.TenantId &&
                entity.ProviderId == provider.Id)
            .ToList()
            .Where(entity => externalDepartments.Contains(entity.ExternalDepartment))
            .Select(entity => entity.LocalDepartmentId)
            .Distinct()
            .ToArray();
        if (mappedDepartmentIds.Length == 0)
        {
            return null;
        }

        return _departmentRepository.QueryForTenant(provider.TenantId)
            .Where(entity =>
                !entity.IsDeleted &&
                entity.TenantId == provider.TenantId &&
                mappedDepartmentIds.Contains(entity.Id) &&
                entity.IsEnabled)
            .OrderBy(entity => entity.Sort)
            .Select(entity => (Guid?)entity.Id)
            .FirstOrDefault();
    }

    private IReadOnlyCollection<Guid> ResolveDefaultRoleIds(SsoProvider provider)
    {
        if (string.IsNullOrWhiteSpace(provider.DefaultRoleIds))
        {
            return [];
        }

        var roleIds = provider.DefaultRoleIds
            .Split([',', ';', '|', ' ', '\r', '\n', '\t', '[', ']', '"'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var roleId) ? roleId : Guid.Empty)
            .Where(roleId => roleId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (roleIds.Length == 0)
        {
            return [];
        }

        var roles = GetAssignableRoles(provider.TenantId, roleIds);
        return roles.Select(entity => entity.Id).ToArray();
    }

    private IReadOnlyCollection<Role> GetAssignableRoles(Guid tenantId, IReadOnlyCollection<Guid> roleIds)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        var roles = _roleRepository.QueryForTenant(tenantId)
            .Where(entity => !entity.IsDeleted && entity.TenantId == tenantId && roleIds.Contains(entity.Id) && entity.IsEnabled)
            .ToList();
        if (roles.Any(role => string.Equals(role.Code, SystemBuiltinConstants.SuperAdminRoleCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(ErrorCode.Forbidden, "SSO cannot automatically assign SuperAdmin role.");
        }

        return roles;
    }

    private AuthenticatedUser BuildAuthenticatedUser(User user)
    {
        var userRoleIds = _userRoleRepository.QueryForTenant(user.TenantId)
            .Where(entity => !entity.IsDeleted && entity.TenantId == user.TenantId && entity.UserId == user.Id)
            .Select(entity => entity.RoleId)
            .ToArray();
        var roles = _roleRepository.QueryForTenant(user.TenantId)
            .Where(entity => !entity.IsDeleted && entity.TenantId == user.TenantId && userRoleIds.Contains(entity.Id) && entity.IsEnabled)
            .ToList();
        var roleIds = roles.Select(entity => entity.Id).ToArray();
        var roleCodes = roles.Select(entity => entity.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissionIds = _rolePermissionRepository.QueryForTenant(user.TenantId)
            .Where(entity => !entity.IsDeleted && entity.TenantId == user.TenantId && roleIds.Contains(entity.RoleId))
            .Select(entity => entity.PermissionId)
            .Distinct()
            .ToArray();
        var permissionCodes = _permissionRepository.QueryForTenant(user.TenantId)
            .Where(entity => !entity.IsDeleted && entity.TenantId == user.TenantId && permissionIds.Contains(entity.Id))
            .Select(entity => entity.Code)
            .Distinct()
            .ToArray();

        return new AuthenticatedUser(
            user.Id,
            user.UserName,
            user.TenantId,
            user.DepartmentId,
            user.SecurityStamp,
            roleCodes,
            permissionCodes);
    }

    private void UpdateBinding(SsoUserBinding binding, ExternalSsoUser externalUser)
    {
        binding.ExternalUserName = NormalizeOptional(externalUser.UserName);
        binding.ExternalEmail = NormalizeOptional(externalUser.Email);
        binding.ExternalPhone = NormalizeOptional(externalUser.Phone);
        binding.LastLoginAt = DateTimeOffset.UtcNow;
        binding.ClaimsJson = JsonSerializer.Serialize(externalUser.Claims, JsonOptions);
        _bindingRepository.Update(binding);
    }

    private async Task WriteLogAsync(
        SsoProvider? provider,
        ExternalSsoUser? externalUser,
        User? localUser,
        SsoLoginResult result,
        string? failureReason,
        SsoLoginContext context,
        CancellationToken cancellationToken)
    {
        await _loginLogRepository.AddAsync(new SsoLoginLog
        {
            TenantId = provider?.TenantId ?? localUser?.TenantId ?? _tenantContext.TenantId ?? Guid.Empty,
            ProviderCode = provider?.ProviderCode ?? string.Empty,
            ProviderName = provider?.ProviderName ?? string.Empty,
            ProviderType = provider?.ProviderType ?? SsoProviderType.Oidc,
            ExternalUserId = externalUser?.ExternalUserId,
            ExternalUserName = externalUser?.UserName,
            LocalUserId = localUser?.Id,
            LocalUserName = localUser?.UserName,
            LoginResult = result,
            FailureReason = failureReason,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent,
            TraceId = context.TraceId
        }, cancellationToken);
    }

    private string GenerateAvailableUserName(Guid tenantId, ExternalSsoUser externalUser)
    {
        var baseUserName = NormalizeUserName(externalUser.UserName) ??
            NormalizeUserName(externalUser.Email?.Split('@')[0]) ??
            NormalizeUserName("sso-" + externalUser.ExternalUserId) ??
            "sso-user";
        baseUserName = baseUserName.Length <= 48 ? baseUserName : baseUserName[..48];

        var candidate = baseUserName;
        var suffix = 1;
        while (_userRepository.QueryForTenant(tenantId).Any(entity =>
            !entity.IsDeleted &&
            entity.TenantId == tenantId &&
            entity.NormalizedUserName == candidate.ToUpperInvariant()))
        {
            candidate = $"{baseUserName}-{suffix++}";
            if (candidate.Length > 64)
            {
                candidate = candidate[..64];
            }
        }

        return candidate;
    }

    private static string? NormalizeUserName(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return null;
        }

        var chars = normalized
            .Where(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.')
            .ToArray();
        var result = new string(chars).Trim('.', '-', '_');
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string GenerateRandomPassword()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    private static string GenerateLoginCode()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string BuildLoginCodeCacheKey(string loginCode)
    {
        return $"ps:sso:login-code:{loginCode}";
    }

    private static SsoLoginCodeCacheEntry ToCacheEntry(AuthenticatedUser user)
    {
        return new SsoLoginCodeCacheEntry
        {
            UserId = user.UserId,
            Username = user.Username,
            TenantId = user.TenantId,
            DepartmentId = user.DepartmentId,
            SecurityStamp = user.SecurityStamp,
            Roles = user.Roles,
            PermissionCodes = user.PermissionCodes
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlySet<string> NormalizeExternalValues(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static SsoLoginResult ResolveLoginResult(string message)
    {
        if (message.Contains("Tenant is disabled", StringComparison.OrdinalIgnoreCase))
        {
            return SsoLoginResult.TenantDisabled;
        }

        if (message.Contains("user is disabled", StringComparison.OrdinalIgnoreCase))
        {
            return SsoLoginResult.UserDisabled;
        }

        if (message.Contains("not bound", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("matched", StringComparison.OrdinalIgnoreCase))
        {
            return SsoLoginResult.BindingFailed;
        }

        if (message.Contains("create", StringComparison.OrdinalIgnoreCase))
        {
            return SsoLoginResult.AutoCreateFailed;
        }

        return SsoLoginResult.Failed;
    }

    private void EnsureOidcEnabled()
    {
        if (!_ssoConfiguration.Enabled)
        {
            throw new BusinessException(ErrorCode.Forbidden, "SSO is disabled globally.");
        }

        if (!_ssoConfiguration.EnableOidc)
        {
            throw new BusinessException(ErrorCode.Forbidden, "OIDC SSO is disabled globally.");
        }
    }
}
