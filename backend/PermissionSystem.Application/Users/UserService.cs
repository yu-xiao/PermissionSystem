using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.Excels;
using PermissionSystem.Application.Security;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using Microsoft.Extensions.Logging;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Users;

public sealed class UserService : IUserService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IExcelService _excelService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly ICacheService _cacheService;
    private readonly ISecurityPolicyService _securityPolicyService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly ILogger<UserService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAsyncQueryExecutor _asyncQueryExecutor;

    public UserService(
        IRepository<User> userRepository,
        IRepository<Role> roleRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<Department> departmentRepository,
        IPasswordHashService passwordHashService,
        IExcelService excelService,
        ICurrentUserService currentUserService,
        ITenantWriteResolver tenantWriteResolver,
        ICacheService cacheService,
        ISecurityPolicyService securityPolicyService,
        IUserSessionService userSessionService,
        ITokenRevocationService tokenRevocationService,
        ILogger<UserService> logger,
        IUnitOfWork unitOfWork,
        IAsyncQueryExecutor asyncQueryExecutor)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _departmentRepository = departmentRepository;
        _passwordHashService = passwordHashService;
        _excelService = excelService;
        _currentUserService = currentUserService;
        _tenantWriteResolver = tenantWriteResolver;
        _cacheService = cacheService;
        _securityPolicyService = securityPolicyService;
        _userSessionService = userSessionService;
        _tokenRevocationService = tokenRevocationService;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _asyncQueryExecutor = asyncQueryExecutor;
    }

    public async Task<PagedResult<UserResponse>> GetPagedAsync(UserQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = ApplyQuery(request);

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var users = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderByDescending(entity => entity.CreatedAt)
                .Skip(request.Skip)
                .Take(request.PageSize)
                .Select(entity => new
                {
                    entity.Id,
                    entity.TenantId,
                    entity.DepartmentId,
                    entity.UserName,
                    entity.NormalizedUserName,
                    entity.DisplayName,
                    entity.Email,
                    entity.PhoneNumber,
                    entity.IsEnabled,
                    entity.IsBuiltin,
                    entity.CreatedAt,
                    entity.RowVersion
                }),
            cancellationToken);
        var userIds = users.Select(entity => entity.Id).ToArray();
        var roleAssignments = userIds.Length == 0
            ? []
            : await _asyncQueryExecutor.ToListAsync(
                from relation in _userRoleRepository.Query()
                join role in _roleRepository.Query() on relation.RoleId equals role.Id
                where userIds.Contains(relation.UserId)
                select new
                {
                    relation.UserId,
                    relation.RoleId,
                    role.Code
                },
                cancellationToken);
        var rolesByUserId = roleAssignments
            .GroupBy(entity => entity.UserId)
            .ToDictionary(entity => entity.Key, entity => entity.ToArray());
        var result = users.Select(user =>
        {
            var roles = rolesByUserId.GetValueOrDefault(user.Id) ?? [];
            var roleCodes = roles.Select(entity => entity.Code).ToArray();
            return new UserResponse
            {
                Id = user.Id,
                TenantId = user.TenantId,
                DepartmentId = user.DepartmentId,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsEnabled = user.IsEnabled,
                IsBuiltin = user.IsBuiltin ||
                    string.Equals(user.UserName, SystemBuiltinConstants.AdminUserName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(user.NormalizedUserName, SystemBuiltinConstants.AdminNormalizedUserName, StringComparison.OrdinalIgnoreCase),
                IsSuperAdmin = roleCodes.Contains(SystemBuiltinConstants.SuperAdminRoleCode, StringComparer.OrdinalIgnoreCase),
                IsCurrentUser = _currentUserService.UserId == user.Id,
                CreatedAt = user.CreatedAt,
                ConcurrencyToken = user.RowVersion,
                RoleIds = roles.Select(entity => entity.RoleId).ToArray(),
                RoleCodes = roleCodes
            };
        }).ToList();

        return PagedResult<UserResponse>.Create(result, request.PageIndex, request.PageSize, totalCount);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.UserName, "Username is required.");
        ValidateRequired(request.Password, "Password is required.");
        ValidateRequired(request.DisplayName, "Display name is required.");
        await _securityPolicyService.ValidatePasswordAsync(request.Password, cancellationToken);

        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        if (request.DepartmentId.HasValue)
        {
            var department = await _departmentRepository.GetByIdAsync(request.DepartmentId.Value, cancellationToken);
            if (department is null || department.TenantId != tenantId)
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Department is invalid for the target tenant.");
            }
        }

        var normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        if (await _asyncQueryExecutor.AnyAsync(
                _userRepository.Query().Where(entity => entity.TenantId == tenantId && entity.NormalizedUserName == normalizedUserName),
                cancellationToken))
        {
            throw new BusinessException(ErrorCode.Conflict, "Username already exists.");
        }

        var user = new User
        {
            TenantId = tenantId,
            DepartmentId = request.DepartmentId,
            UserName = request.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = _passwordHashService.HashPassword(request.Password),
            IsEnabled = request.IsEnabled
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToResponseAsync(user, cancellationToken);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(user, request.ConcurrencyToken);
        await EnsureCanUpdateUserAsync(user, request, cancellationToken);
        var authorizationChanged = user.DepartmentId != request.DepartmentId || user.IsEnabled != request.IsEnabled;
        var revokeAuthentication = user.IsEnabled != request.IsEnabled;

        user.DepartmentId = request.DepartmentId;
        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        user.IsEnabled = request.IsEnabled;
        if (authorizationChanged)
        {
            user.RotateSecurityStamp();
        }

        if (revokeAuthentication)
        {
            IReadOnlyCollection<RevokedUserSession> revokedSessions = [];
            await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                _userRepository.Update(user);
                revokedSessions = _userSessionService.StageUserSessionsRevocation(
                    user.Id,
                    request.IsEnabled ? "User re-enabled." : "User disabled.");
                await _unitOfWork.SaveChangesAsync(token);
                await _tokenRevocationService.RevokeUserRefreshTokensAsync(user.Id, token);
            }, cancellationToken);
            await _userSessionService.PublishRevokedSessionsAsync(revokedSessions, cancellationToken);
        }
        else
        {
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await RemoveUserAuthorizationCachesAsync(user.TenantId, user.Id, cancellationToken);

        return await ToResponseAsync(user, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);
        await EnsureCanDeleteUserAsync(user, cancellationToken);
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("user:delete", cancellationToken);

        IReadOnlyCollection<RevokedUserSession> revokedSessions = [];
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var relations = await _asyncQueryExecutor.ToListAsync(
                _userRoleRepository.Query().Where(entity => entity.UserId == id),
                token);
            foreach (var relation in relations)
            {
                _userRoleRepository.Remove(relation);
            }

            user.RotateSecurityStamp();
            _userRepository.Remove(user);
            revokedSessions = _userSessionService.StageUserSessionsRevocation(user.Id, "User deleted.");
            await _unitOfWork.SaveChangesAsync(token);
            await _tokenRevocationService.RevokeUserRefreshTokensAsync(user.Id, token);
        }, cancellationToken);
        await _userSessionService.PublishRevokedSessionsAsync(revokedSessions, cancellationToken);
        await RemoveUserAuthorizationCachesAsync(user.TenantId, user.Id, cancellationToken);
    }

    public async Task SetEnabledAsync(Guid id, SetUserEnabledRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);
        await EnsureCanSetEnabledAsync(user, request.IsEnabled, cancellationToken);
        var changed = user.IsEnabled != request.IsEnabled;
        user.IsEnabled = request.IsEnabled;
        if (changed)
        {
            user.RotateSecurityStamp();
        }

        if (changed)
        {
            IReadOnlyCollection<RevokedUserSession> revokedSessions = [];
            await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                _userRepository.Update(user);
                revokedSessions = _userSessionService.StageUserSessionsRevocation(
                    user.Id,
                    request.IsEnabled ? "User re-enabled." : "User disabled.");
                await _unitOfWork.SaveChangesAsync(token);
                await _tokenRevocationService.RevokeUserRefreshTokensAsync(user.Id, token);
            }, cancellationToken);
            await _userSessionService.PublishRevokedSessionsAsync(revokedSessions, cancellationToken);
        }
        else
        {
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await RemoveUserAuthorizationCachesAsync(user.TenantId, user.Id, cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid id, ResetUserPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.NewPassword, "New password is required.");
        await _securityPolicyService.ValidatePasswordAsync(request.NewPassword, cancellationToken);

        var user = await GetUserOrThrowAsync(id, cancellationToken);
        await EnsureCanResetPasswordAsync(user, cancellationToken);
        await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("user:reset-password", cancellationToken);
        user.PasswordHash = _passwordHashService.HashPassword(request.NewPassword);
        user.RotateSecurityStamp();

        IReadOnlyCollection<RevokedUserSession> revokedSessions = [];
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            _userRepository.Update(user);
            revokedSessions = _userSessionService.StageUserSessionsRevocation(
                user.Id,
                "Password reset by administrator.");
            await _unitOfWork.SaveChangesAsync(token);
            await _tokenRevocationService.RevokeUserRefreshTokensAsync(user.Id, token);
        }, cancellationToken);
        await _userSessionService.PublishRevokedSessionsAsync(revokedSessions, cancellationToken);
        await RemoveUserAuthorizationCachesAsync(user.TenantId, user.Id, cancellationToken);
    }

    public async Task AssignRolesAsync(Guid id, AssignUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);
        var roleIds = request.RoleIds.Distinct().ToArray();
        var validRoles = await _asyncQueryExecutor.ToListAsync(
            _roleRepository.Query()
                .Where(entity => entity.TenantId == user.TenantId && roleIds.Contains(entity.Id)),
            cancellationToken);
        var validRoleIds = validRoles.Select(entity => entity.Id).ToArray();

        if (validRoleIds.Length != roleIds.Length)
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more roles are invalid.");
        }

        await EnsureCanAssignRolesAsync(user, validRoles, cancellationToken);
        if (validRoles.Any(IsSuperAdminRole) || await UserHasSuperAdminRoleAsync(user.Id, cancellationToken))
        {
            await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("user:assign-super-admin", force: true, cancellationToken);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var relations = await _asyncQueryExecutor.ToListAsync(
                _userRoleRepository.Query().Where(entity => entity.UserId == id),
                token);
            foreach (var relation in relations)
            {
                _userRoleRepository.Remove(relation);
            }

            foreach (var roleId in validRoleIds)
            {
                await _userRoleRepository.AddAsync(new UserRole
                {
                    TenantId = user.TenantId,
                    UserId = user.Id,
                    RoleId = roleId
                }, token);
            }

            user.RotateSecurityStamp();
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        await RemoveUserAuthorizationCachesAsync(user.TenantId, user.Id, cancellationToken);
    }

    public async Task<byte[]> ExportAsync(UserQueryRequest request, CancellationToken cancellationToken = default)
    {
        var rows = await _asyncQueryExecutor.ToListAsync(
            ApplyQuery(request)
                .OrderBy(entity => entity.UserName)
                .Select(entity => new UserExportRow
                {
                    UserName = entity.UserName,
                    DisplayName = entity.DisplayName,
                    Email = entity.Email,
                    PhoneNumber = entity.PhoneNumber,
                    IsEnabled = entity.IsEnabled,
                    CreatedAt = entity.CreatedAt
                }),
            cancellationToken);

        return await _excelService.ExportAsync(
            new ExportRequest<UserExportRow>
            {
                SheetName = "Users",
                Items = rows
            },
            cancellationToken);
    }

    public Task<byte[]> CreateImportTemplateAsync(CancellationToken cancellationToken = default)
    {
        return _excelService.CreateTemplateAsync<UserImportRow>("User Import Template", cancellationToken);
    }

    public async Task<ImportResult<UserImportRow>> ImportPreviewAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var result = await _excelService.ImportAsync<UserImportRow>(stream, cancellationToken);
        var errors = result.Errors.ToList();
        var validItems = new List<UserImportRow>();
        var seenUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedUserNames = result.Items
            .Select(entity => entity.UserName.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingUserNames = (await _asyncQueryExecutor.ToListAsync(
                _userRepository.Query()
                    .Where(entity => normalizedUserNames.Contains(entity.NormalizedUserName))
                    .Select(entity => entity.NormalizedUserName),
                cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rowNumber = 1;

        foreach (var item in result.Items)
        {
            rowNumber++;
            var normalizedUserName = item.UserName.Trim().ToUpperInvariant();
            var hasError = false;

            if (!seenUserNames.Add(normalizedUserName))
            {
                errors.Add(new ImportError
                {
                    RowNumber = rowNumber,
                    ColumnName = "Username",
                    Message = "Username is duplicated in the import file.",
                    RawValue = item.UserName
                });
                hasError = true;
            }

            if (existingUserNames.Contains(normalizedUserName))
            {
                errors.Add(new ImportError
                {
                    RowNumber = rowNumber,
                    ColumnName = "Username",
                    Message = "Username already exists.",
                    RawValue = item.UserName
                });
                hasError = true;
            }

            if (!hasError)
            {
                validItems.Add(item);
            }
        }

        return new ImportResult<UserImportRow>
        {
            TotalRows = result.TotalRows,
            SuccessRows = validItems.Count,
            FailedRows = errors.Select(error => error.RowNumber).Distinct().Count(),
            Items = validItems,
            Errors = errors
        };
    }

    private async Task<User> GetUserOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "User was not found.");
    }

    private IQueryable<User> ApplyQuery(UserQueryRequest request)
    {
        var query = _userRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.UserName.Contains(keyword) ||
                entity.DisplayName.Contains(keyword) ||
                (entity.Email != null && entity.Email.Contains(keyword)));
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        return query;
    }

    private async Task<UserResponse> ToResponseAsync(User user, CancellationToken cancellationToken)
    {
        var roles = await _asyncQueryExecutor.ToListAsync(
            from relation in _userRoleRepository.Query()
            join role in _roleRepository.Query() on relation.RoleId equals role.Id
            where relation.UserId == user.Id
            select new { relation.RoleId, role.Code },
            cancellationToken);
        var roleIds = roles.Select(entity => entity.RoleId).ToArray();
        var roleCodes = roles.Select(entity => entity.Code).ToArray();

        return new UserResponse
        {
            Id = user.Id,
            TenantId = user.TenantId,
            DepartmentId = user.DepartmentId,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsEnabled = user.IsEnabled,
            IsBuiltin = IsBuiltinAdminUser(user),
            IsSuperAdmin = roleCodes.Contains(SystemBuiltinConstants.SuperAdminRoleCode, StringComparer.OrdinalIgnoreCase),
            IsCurrentUser = _currentUserService.UserId == user.Id,
            CreatedAt = user.CreatedAt,
            ConcurrencyToken = user.RowVersion,
            RoleIds = roleIds,
            RoleCodes = roleCodes
        };
    }

    private async Task EnsureCanDeleteUserAsync(User user, CancellationToken cancellationToken)
    {
        if (IsBuiltinAdminUser(user))
        {
            RejectDangerousOperation("Blocked deleting builtin admin user {UserId}.", user.Id, "系统内置管理员账号不允许删除。");
        }

        if (IsCurrentUser(user))
        {
            RejectDangerousOperation("Blocked user {UserId} deleting itself.", user.Id, "不能删除当前登录用户。");
        }

        if (await UserHasSuperAdminRoleAsync(user.Id, cancellationToken))
        {
            if (!_currentUserService.IsSuperAdmin)
            {
                RejectDangerousOperation("Blocked non-SuperAdmin deleting SuperAdmin user {UserId}.", user.Id, "无权删除超级管理员用户。");
            }

            await EnsureNotLastSuperAdminUserAsync(user.Id, "不能删除系统最后一个超级管理员用户。", cancellationToken);
        }
    }

    private async Task EnsureCanSetEnabledAsync(User user, bool isEnabled, CancellationToken cancellationToken)
    {
        if (isEnabled)
        {
            return;
        }

        if (IsBuiltinAdminUser(user))
        {
            RejectDangerousOperation("Blocked disabling builtin admin user {UserId}.", user.Id, "系统内置管理员账号不允许禁用。");
        }

        if (IsCurrentUser(user))
        {
            RejectDangerousOperation("Blocked user {UserId} disabling itself.", user.Id, "不能禁用当前登录用户。");
        }

        if (await UserHasSuperAdminRoleAsync(user.Id, cancellationToken))
        {
            if (!_currentUserService.IsSuperAdmin)
            {
                RejectDangerousOperation("Blocked non-SuperAdmin disabling SuperAdmin user {UserId}.", user.Id, "无权禁用超级管理员用户。");
            }

            await EnsureNotLastSuperAdminUserAsync(user.Id, "不能禁用系统最后一个超级管理员用户。", cancellationToken);
        }
    }

    private async Task EnsureCanUpdateUserAsync(User user, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (IsCurrentUser(user) && !request.IsEnabled)
        {
            RejectDangerousOperation("Blocked user {UserId} disabling itself through update.", user.Id, "不能禁用当前登录用户。");
        }

        if (!request.IsEnabled && IsBuiltinAdminUser(user))
        {
            RejectDangerousOperation("Blocked disabling builtin admin user {UserId} through update.", user.Id, "系统内置管理员账号不允许禁用。");
        }

        if ((IsBuiltinAdminUser(user) || await UserHasSuperAdminRoleAsync(user.Id, cancellationToken)) &&
            !_currentUserService.IsSuperAdmin &&
            !IsCurrentUser(user))
        {
            RejectDangerousOperation("Blocked non-SuperAdmin updating protected user {UserId}.", user.Id, "无权修改系统内置或超级管理员用户。");
        }
    }

    private async Task EnsureCanResetPasswordAsync(User user, CancellationToken cancellationToken)
    {
        if (IsCurrentUser(user))
        {
            RejectDangerousOperation("Blocked user {UserId} resetting its own password through user management.", user.Id, "当前用户请通过个人中心修改密码。");
        }

        if (IsBuiltinAdminUser(user))
        {
            RejectDangerousOperation("Blocked resetting builtin admin password {UserId}.", user.Id, "系统内置管理员账号密码不允许通过用户管理重置。");
        }

        if (await UserHasSuperAdminRoleAsync(user.Id, cancellationToken) && !_currentUserService.IsSuperAdmin)
        {
            RejectDangerousOperation("Blocked non-SuperAdmin resetting SuperAdmin user password {UserId}.", user.Id, "无权重置超级管理员用户密码。");
        }
    }

    private async Task EnsureCanAssignRolesAsync(
        User user,
        IReadOnlyCollection<Role> newRoles,
        CancellationToken cancellationToken)
    {
        var newRoleIds = newRoles.Select(entity => entity.Id).ToHashSet();
        var newHasSuperAdmin = newRoles.Any(IsSuperAdminRole);
        var oldHasSuperAdmin = await UserHasSuperAdminRoleAsync(user.Id, cancellationToken);
        var superAdminRole = await _asyncQueryExecutor.FirstOrDefaultAsync(
            _roleRepository.Query().Where(entity => entity.Code == SystemBuiltinConstants.SuperAdminRoleCode),
            cancellationToken);

        if (newHasSuperAdmin && !_currentUserService.IsSuperAdmin)
        {
            RejectDangerousOperation("Blocked non-SuperAdmin assigning SuperAdmin role to user {UserId}.", user.Id, "无权分配超级管理员角色。");
        }

        if (oldHasSuperAdmin && !newHasSuperAdmin && !_currentUserService.IsSuperAdmin)
        {
            RejectDangerousOperation("Blocked non-SuperAdmin removing SuperAdmin role from user {UserId}.", user.Id, "无权移除超级管理员角色。");
        }

        if (IsBuiltinAdminUser(user) && superAdminRole is not null && !newRoleIds.Contains(superAdminRole.Id))
        {
            RejectDangerousOperation("Blocked removing SuperAdmin role from builtin admin user {UserId}.", user.Id, "admin 用户必须始终保留超级管理员角色。");
        }

        if (IsCurrentUser(user) && oldHasSuperAdmin && !newHasSuperAdmin)
        {
            RejectDangerousOperation("Blocked current user {UserId} removing its own SuperAdmin role.", user.Id, "不能移除当前登录用户自己的超级管理员角色。");
        }

        if (oldHasSuperAdmin && !newHasSuperAdmin)
        {
            await EnsureNotLastSuperAdminUserAsync(user.Id, "不能移除系统最后一个超级管理员用户。", cancellationToken);
        }
    }

    private Task<bool> UserHasSuperAdminRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var query =
            from relation in _userRoleRepository.Query()
            join role in _roleRepository.Query() on relation.RoleId equals role.Id
            where relation.UserId == userId && role.Code == SystemBuiltinConstants.SuperAdminRoleCode
            select relation.Id;
        return _asyncQueryExecutor.AnyAsync(query, cancellationToken);
    }

    private async Task EnsureNotLastSuperAdminUserAsync(
        Guid removedUserId,
        string message,
        CancellationToken cancellationToken)
    {
        var query = (
            from relation in _userRoleRepository.Query()
            join role in _roleRepository.Query() on relation.RoleId equals role.Id
            where relation.UserId != removedUserId && role.Code == SystemBuiltinConstants.SuperAdminRoleCode
            select relation.UserId)
            .Distinct();
        var remainingCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);

        if (remainingCount == 0)
        {
            RejectDangerousOperation("Blocked removing the last SuperAdmin user {UserId}.", removedUserId, message);
        }
    }

    private static bool IsSuperAdminRole(Role role)
    {
        return string.Equals(role.Code, SystemBuiltinConstants.SuperAdminRoleCode, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBuiltinAdminUser(User user)
    {
        return user.IsBuiltin ||
            string.Equals(user.UserName, SystemBuiltinConstants.AdminUserName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.NormalizedUserName, SystemBuiltinConstants.AdminNormalizedUserName, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCurrentUser(User user)
    {
        return _currentUserService.UserId == user.Id;
    }

    private async Task RemoveUserAuthorizationCachesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _cacheService.RemoveAsync(BuildUserMenusCacheKey(tenantId, userId), cancellationToken);
        await _cacheService.RemoveAsync(BuildUserPermissionsCacheKey(tenantId, userId), cancellationToken);
        await _cacheService.RemoveAsync(BuildUserRolesCacheKey(tenantId, userId), cancellationToken);
    }

    private void RejectDangerousOperation(string logMessage, Guid targetUserId, string businessMessage)
    {
        _logger.LogWarning(
            logMessage,
            targetUserId,
            _currentUserService.UserId,
            _currentUserService.Username);
        throw new BusinessException(ErrorCode.Forbidden, businessMessage);
    }

    private static string BuildUserMenusCacheKey(Guid tenantId, Guid userId)
    {
        return $"ps:user-menus:{tenantId}:{userId}";
    }

    private static string BuildUserPermissionsCacheKey(Guid tenantId, Guid userId)
    {
        return $"ps:user-permissions:{tenantId}:{userId}";
    }

    private static string BuildUserRolesCacheKey(Guid tenantId, Guid userId)
    {
        return $"ps:user-roles:{tenantId}:{userId}";
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }
}
