using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Security;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using Microsoft.Extensions.Logging;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Roles;

public sealed class RoleService : IRoleService
{
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<RoleMenu> _roleMenuRepository;
    private readonly IRepository<RolePermission> _rolePermissionRepository;
    private readonly IRepository<Menu> _menuRepository;
    private readonly IRepository<Domain.Entities.Permission> _permissionRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<RoleDataScope> _roleDataScopeRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly ICacheService _cacheService;
    private readonly ISecurityPolicyService _securityPolicyService;
    private readonly ILogger<RoleService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public RoleService(
        IRepository<Role> roleRepository,
        IRepository<RoleMenu> roleMenuRepository,
        IRepository<RolePermission> rolePermissionRepository,
        IRepository<Menu> menuRepository,
        IRepository<Domain.Entities.Permission> permissionRepository,
        IRepository<User> userRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<RoleDataScope> roleDataScopeRepository,
        IRepository<Department> departmentRepository,
        ICurrentUserService currentUserService,
        ITenantWriteResolver tenantWriteResolver,
        ICacheService cacheService,
        ISecurityPolicyService securityPolicyService,
        ILogger<RoleService> logger,
        IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _roleMenuRepository = roleMenuRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _menuRepository = menuRepository;
        _permissionRepository = permissionRepository;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleDataScopeRepository = roleDataScopeRepository;
        _departmentRepository = departmentRepository;
        _currentUserService = currentUserService;
        _tenantWriteResolver = tenantWriteResolver;
        _cacheService = cacheService;
        _securityPolicyService = securityPolicyService;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<RoleResponse>> GetPagedAsync(RoleQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = _roleRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity => entity.Code.Contains(keyword) || entity.Name.Contains(keyword));
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var roles = query
            .OrderBy(entity => entity.Sort)
            .ThenByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<RoleResponse>.Create(roles, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Code, "Role code is required.");
        ValidateRequired(request.Name, "Role name is required.");

        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        var code = request.Code.Trim();
        if (_roleRepository.Query().Any(entity => entity.TenantId == tenantId && entity.Code == code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Role code already exists.");
        }

        var role = new Role
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description,
            IsEnabled = request.IsEnabled,
            Sort = request.Sort
        };

        await _roleRepository.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(role);
    }

    public async Task<RoleResponse> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(id, cancellationToken);
        EnsureCanUpdateRole(role, request);
        var authorizationChanged = role.IsEnabled != request.IsEnabled;
        var affectedUserIds = authorizationChanged
            ? GetRoleUserIds(role.TenantId, role.Id)
            : [];

        role.Name = request.Name.Trim();
        role.Description = request.Description;
        role.IsEnabled = request.IsEnabled;
        role.Sort = request.Sort;

        RotateUserSecurityStamps(role.TenantId, affectedUserIds);

        _roleRepository.Update(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (authorizationChanged)
        {
            await RemoveRoleUserCachesAsync(role.TenantId, affectedUserIds, cancellationToken);
        }

        return ToResponse(role);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(id, cancellationToken);
        EnsureCanDeleteRole(role);
        var affectedUserIds = GetRoleUserIds(role.TenantId, role.Id);

        foreach (var relation in _userRoleRepository.Query().Where(entity => entity.RoleId == id).ToList())
        {
            _userRoleRepository.Remove(relation);
        }

        foreach (var relation in _roleMenuRepository.Query().Where(entity => entity.RoleId == id).ToList())
        {
            _roleMenuRepository.Remove(relation);
        }

        foreach (var relation in _rolePermissionRepository.Query().Where(entity => entity.RoleId == id).ToList())
        {
            _rolePermissionRepository.Remove(relation);
        }

        _roleRepository.Remove(role);
        RotateUserSecurityStamps(role.TenantId, affectedUserIds);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveRoleUserCachesAsync(role.TenantId, affectedUserIds, cancellationToken);
    }

    public async Task AssignMenusAsync(Guid id, AssignRoleMenusRequest request, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(id, cancellationToken);
        EnsureCanModifyRoleAuthorization(role);
        if (IsSuperAdminRole(role))
        {
            await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("role:super-admin-permission:update", force: true, cancellationToken);
        }

        var menuIds = request.MenuIds.Distinct().ToArray();
        var validMenuIds = _menuRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && menuIds.Contains(entity.Id))
            .Select(entity => entity.Id)
            .ToArray();

        if (validMenuIds.Length != menuIds.Length)
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more menus are invalid.");
        }

        foreach (var relation in _roleMenuRepository.Query().Where(entity => entity.RoleId == id).ToList())
        {
            _roleMenuRepository.Remove(relation);
        }

        foreach (var menuId in validMenuIds)
        {
            await _roleMenuRepository.AddAsync(new RoleMenu
            {
                TenantId = role.TenantId,
                RoleId = role.Id,
                MenuId = menuId
            }, cancellationToken);
        }

        RotateRoleUserSecurityStamps(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveRolePermissionCachesAsync(role, cancellationToken);
    }

    public async Task AssignPermissionsAsync(Guid id, AssignRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(id, cancellationToken);
        EnsureCanModifyRoleAuthorization(role);
        if (IsSuperAdminRole(role))
        {
            await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("role:super-admin-permission:update", force: true, cancellationToken);
        }

        var permissionIds = request.PermissionIds.Distinct().ToArray();
        var validPermissionIds = _permissionRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && permissionIds.Contains(entity.Id))
            .Select(entity => entity.Id)
            .ToArray();

        if (validPermissionIds.Length != permissionIds.Length)
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more permissions are invalid.");
        }

        foreach (var relation in _rolePermissionRepository.Query().Where(entity => entity.RoleId == id).ToList())
        {
            _rolePermissionRepository.Remove(relation);
        }

        foreach (var permissionId in validPermissionIds)
        {
            await _rolePermissionRepository.AddAsync(new RolePermission
            {
                TenantId = role.TenantId,
                RoleId = role.Id,
                PermissionId = permissionId
            }, cancellationToken);
        }

        RotateRoleUserSecurityStamps(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RemoveRolePermissionCachesAsync(role, cancellationToken);
    }

    public async Task<RoleUsersResponse> GetRoleUsersAsync(
        Guid roleId,
        RoleUsersQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var role = await GetRoleOrThrowAsync(roleId, cancellationToken);
        var selectedUserIds = _userRoleRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && entity.RoleId == role.Id)
            .Select(entity => entity.UserId)
            .Distinct()
            .ToHashSet();
        var query = _userRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.UserName.Contains(keyword) ||
                entity.DisplayName.Contains(keyword) ||
                (entity.PhoneNumber != null && entity.PhoneNumber.Contains(keyword)) ||
                (entity.Email != null && entity.Email.Contains(keyword)));
        }

        var totalCount = query.LongCount();
        var users = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList();
        var departmentIds = users
            .Where(entity => entity.DepartmentId.HasValue)
            .Select(entity => entity.DepartmentId!.Value)
            .Distinct()
            .ToArray();
        var departmentsById = _departmentRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && departmentIds.Contains(entity.Id))
            .ToDictionary(entity => entity.Id, entity => entity.Name);
        var items = users.Select(user => new RoleUserResponse
        {
            UserId = user.Id,
            UserName = user.UserName,
            NickName = user.DisplayName,
            RealName = user.DisplayName,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            DepartmentName = user.DepartmentId.HasValue && departmentsById.TryGetValue(user.DepartmentId.Value, out var departmentName)
                ? departmentName
                : null,
            Status = user.IsEnabled ? "Enabled" : "Disabled",
            Checked = selectedUserIds.Contains(user.Id)
        }).ToList();

        return new RoleUsersResponse
        {
            SelectedUserIds = selectedUserIds.ToArray(),
            Users = PagedResult<RoleUserResponse>.Create(items, request.PageIndex, request.PageSize, totalCount)
        };
    }

    public async Task SaveRoleUsersAsync(
        Guid roleId,
        SaveRoleUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(roleId, cancellationToken);
        EnsureCanModifyRoleUsers(role);
        if (IsSuperAdminRole(role))
        {
            await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("role:super-admin-users:update", force: true, cancellationToken);
        }

        var userIds = request.UserIds.Distinct().ToArray();
        var validUsers = _userRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && userIds.Contains(entity.Id) && entity.IsEnabled)
            .ToArray();

        if (validUsers.Length != userIds.Length)
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more users are invalid, disabled, or outside the role tenant.");
        }

        EnsureProtectedRoleUsers(role, validUsers);

        var oldUserIds = _userRoleRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && entity.RoleId == role.Id)
            .Select(entity => entity.UserId)
            .Distinct()
            .ToArray();
        var affectedUserIds = oldUserIds
            .Concat(validUsers.Select(entity => entity.Id))
            .Distinct()
            .ToArray();

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            foreach (var relation in _userRoleRepository.Query().Where(entity => entity.RoleId == role.Id).ToList())
            {
                _userRoleRepository.Remove(relation);
            }

            foreach (var userId in validUsers.Select(entity => entity.Id).OrderBy(entity => entity))
            {
                await _userRoleRepository.AddAsync(new UserRole
                {
                    TenantId = role.TenantId,
                    UserId = userId,
                    RoleId = role.Id
                }, token);
            }

            RotateUserSecurityStamps(role.TenantId, affectedUserIds);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        await RemoveRoleUserCachesAsync(role.TenantId, affectedUserIds, cancellationToken);
    }

    public async Task<RolePermissionMatrixResponse> GetPermissionMatrixAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(roleId, cancellationToken);
        var menus = _menuRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId)
            .OrderBy(entity => entity.Sort)
            .ToList();
        var permissions = _permissionRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId)
            .ToList();
        var checkedMenuIds = _roleMenuRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && entity.RoleId == role.Id)
            .Select(entity => entity.MenuId)
            .ToHashSet();
        var checkedPermissionIds = _rolePermissionRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && entity.RoleId == role.Id)
            .Select(entity => entity.PermissionId)
            .ToHashSet();
        var dataScopeSummary = BuildDataScopeSummary(role);
        var rowsByParentId = menus
            .Where(entity => entity.ParentId.HasValue)
            .GroupBy(entity => entity.ParentId!.Value)
            .ToDictionary(entity => entity.Key, entity => entity.OrderBy(menu => menu.Sort).ToList());
        var modules = menus
            .Where(entity => !entity.ParentId.HasValue)
            .OrderBy(entity => entity.Sort)
            .Select(module => BuildModule(
                module,
                rowsByParentId,
                permissions,
                menus,
                checkedMenuIds,
                checkedPermissionIds,
                dataScopeSummary))
            .ToList();

        return new RolePermissionMatrixResponse
        {
            RoleId = role.Id,
            RoleName = role.Name,
            Modules = modules
        };
    }

    public async Task SavePermissionMatrixAsync(
        Guid roleId,
        SaveRolePermissionMatrixRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(roleId, cancellationToken);
        EnsureCanModifyRoleAuthorization(role);
        if (IsSuperAdminRole(role))
        {
            await _securityPolicyService.EnsureSensitiveOperationVerifiedAsync("role:super-admin-permission:update", force: true, cancellationToken);
        }

        var allMenus = _menuRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId)
            .ToList();
        var allPermissions = _permissionRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId)
            .ToList();
        var menuIds = request.MenuIds.Distinct().ToHashSet();
        var permissionIds = request.PermissionIds.Distinct().ToHashSet();

        ValidateMenuIds(menuIds, allMenus);
        ValidatePermissionIds(permissionIds, allPermissions);
        ValidateReservedFieldPermissions(request.FieldPermissions, allMenus);

        var resolvedMenuIds = ResolveMenuIds(
            menuIds,
            permissionIds,
            allMenus,
            allPermissions);
        var resolvedPermissionIds = ResolvePermissionIds(permissionIds, allPermissions);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            foreach (var relation in _roleMenuRepository.Query().Where(entity => entity.RoleId == role.Id).ToList())
            {
                _roleMenuRepository.Remove(relation);
            }

            foreach (var relation in _rolePermissionRepository.Query().Where(entity => entity.RoleId == role.Id).ToList())
            {
                _rolePermissionRepository.Remove(relation);
            }

            foreach (var menuId in resolvedMenuIds.OrderBy(entity => entity))
            {
                await _roleMenuRepository.AddAsync(new RoleMenu
                {
                    TenantId = role.TenantId,
                    RoleId = role.Id,
                    MenuId = menuId
                }, token);
            }

            foreach (var permissionId in resolvedPermissionIds.OrderBy(entity => entity))
            {
                await _rolePermissionRepository.AddAsync(new RolePermission
                {
                    TenantId = role.TenantId,
                    RoleId = role.Id,
                    PermissionId = permissionId
                }, token);
            }

            await ValidateAndApplyDataScopeAsync(role, request.DataScopes, allMenus, token);
            RotateRoleUserSecurityStamps(role);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        await RemoveRolePermissionCachesAsync(role, cancellationToken);
    }

    private async Task<Role> GetRoleOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _roleRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Role was not found.");
    }

    private static RoleResponse ToResponse(Role role)
    {
        return new RoleResponse
        {
            Id = role.Id,
            TenantId = role.TenantId,
            Code = role.Code,
            Name = role.Name,
            Description = role.Description,
            IsEnabled = role.IsEnabled,
            IsBuiltin = role.IsBuiltin,
            IsSuperAdminRole = IsSuperAdminRole(role),
            Sort = role.Sort,
            CreatedAt = role.CreatedAt
        };
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }

    private void EnsureCanDeleteRole(Role role)
    {
        if (IsProtectedRole(role))
        {
            RejectDangerousOperation("Blocked deleting protected role {RoleId}.", role.Id, "系统内置角色或超级管理员角色不允许删除。");
        }
    }

    private void EnsureCanUpdateRole(Role role, UpdateRoleRequest request)
    {
        if (!request.IsEnabled && IsProtectedRole(role))
        {
            RejectDangerousOperation("Blocked disabling protected role {RoleId}.", role.Id, "系统内置角色或超级管理员角色不允许禁用。");
        }

        if (IsProtectedRole(role) && !_currentUserService.IsSuperAdmin)
        {
            RejectDangerousOperation("Blocked non-SuperAdmin updating protected role {RoleId}.", role.Id, "无权修改系统内置角色或超级管理员角色。");
        }

        if (IsProtectedRole(role) && !string.Equals(request.Name.Trim(), role.Name, StringComparison.Ordinal))
        {
            RejectDangerousOperation("Blocked renaming protected role {RoleId}.", role.Id, "系统内置角色名称不允许修改。");
        }
    }

    private void EnsureCanModifyRoleAuthorization(Role role)
    {
        if (!IsProtectedRole(role))
        {
            return;
        }

        if (IsSuperAdminRole(role) && _currentUserService.IsSuperAdmin)
        {
            return;
        }

        if (IsProtectedRole(role))
        {
            RejectDangerousOperation("Blocked modifying protected role authorization {RoleId}.", role.Id, "系统内置角色或超级管理员角色权限不允许修改。");
        }
    }

    private void EnsureCanModifyRoleUsers(Role role)
    {
        if (IsProtectedRole(role) && !_currentUserService.IsSuperAdmin)
        {
            RejectDangerousOperation("Blocked non-SuperAdmin modifying protected role users {RoleId}.", role.Id, "无权修改超级管理员角色关联用户。");
        }
    }

    private void EnsureProtectedRoleUsers(Role role, IReadOnlyCollection<User> users)
    {
        if (!IsSuperAdminRole(role))
        {
            return;
        }

        if (users.Count == 0)
        {
            RejectDangerousOperation("Blocked clearing all SuperAdmin role users {RoleId}.", role.Id, "超级管理员角色至少需要保留一个用户。");
        }

        if (!users.Any(IsBuiltinAdminUser))
        {
            RejectDangerousOperation("Blocked removing admin from SuperAdmin role {RoleId}.", role.Id, "admin 用户必须始终保留超级管理员角色。");
        }
    }

    private static bool IsProtectedRole(Role role)
    {
        return role.IsBuiltin || IsSuperAdminRole(role);
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

    private void RejectDangerousOperation(string logMessage, Guid targetRoleId, string businessMessage)
    {
        _logger.LogWarning(
            logMessage,
            targetRoleId,
            _currentUserService.UserId,
            _currentUserService.Username);
        throw new BusinessException(ErrorCode.Forbidden, businessMessage);
    }

    private static void ValidateMenuIds(HashSet<Guid> menuIds, IReadOnlyCollection<Menu> allMenus)
    {
        var validMenuIds = allMenus.Select(entity => entity.Id).ToHashSet();
        if (!menuIds.All(validMenuIds.Contains))
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more menus are invalid.");
        }
    }

    private static void ValidatePermissionIds(
        HashSet<Guid> permissionIds,
        IReadOnlyCollection<Domain.Entities.Permission> allPermissions)
    {
        var validPermissionIds = allPermissions.Select(entity => entity.Id).ToHashSet();
        if (!permissionIds.All(validPermissionIds.Contains))
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more permissions are invalid.");
        }
    }

    private static void ValidateReservedFieldPermissions(
        IReadOnlyCollection<RoleFieldPermissionRequest> fieldPermissions,
        IReadOnlyCollection<Menu> allMenus)
    {
        if (fieldPermissions.Count == 0)
        {
            return;
        }

        var validMenuIds = allMenus.Select(entity => entity.Id).ToHashSet();
        if (fieldPermissions.Any(entity => !validMenuIds.Contains(entity.MenuId)))
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more field permission menus are invalid.");
        }
    }

    private static HashSet<Guid> ResolveMenuIds(
        HashSet<Guid> selectedMenuIds,
        HashSet<Guid> selectedPermissionIds,
        IReadOnlyCollection<Menu> allMenus,
        IReadOnlyCollection<Domain.Entities.Permission> allPermissions)
    {
        var resolvedMenuIds = new HashSet<Guid>(selectedMenuIds);
        var menusById = allMenus.ToDictionary(entity => entity.Id);
        var resourceUsage = allMenus
            .Select(menu => GetPermissionResource(menu.PermissionCode))
            .Where(resource => !string.IsNullOrWhiteSpace(resource))
            .GroupBy(resource => resource!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var permission in allPermissions.Where(entity => selectedPermissionIds.Contains(entity.Id)))
        {
            var menu = ResolveMenuForPermission(permission, allMenus, resourceUsage);
            if (menu is null)
            {
                throw new BusinessException(ErrorCode.BadRequest, $"Permission '{permission.Code}' cannot be mapped to a menu.");
            }

            AddMenuAndAncestors(menu, menusById, resolvedMenuIds);
        }

        foreach (var menuId in selectedMenuIds.ToArray())
        {
            if (menusById.TryGetValue(menuId, out var menu))
            {
                AddMenuAndAncestors(menu, menusById, resolvedMenuIds);
            }
        }

        return resolvedMenuIds;
    }

    private static HashSet<Guid> ResolvePermissionIds(
        HashSet<Guid> selectedPermissionIds,
        IReadOnlyCollection<Domain.Entities.Permission> allPermissions)
    {
        var resolvedPermissionIds = new HashSet<Guid>(selectedPermissionIds);
        var permissionsByCode = allPermissions.ToDictionary(entity => entity.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var permission in allPermissions.Where(entity => selectedPermissionIds.Contains(entity.Id)).ToList())
        {
            if (string.Equals(permission.Action, "view", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(permission.Resource))
            {
                continue;
            }

            var viewPermissionCode = $"{permission.Resource}:view";
            if (permissionsByCode.TryGetValue(viewPermissionCode, out var viewPermission))
            {
                resolvedPermissionIds.Add(viewPermission.Id);
            }
        }

        return resolvedPermissionIds;
    }

    private async Task ValidateAndApplyDataScopeAsync(
        Role role,
        IReadOnlyCollection<RoleMenuDataScopeRequest> dataScopes,
        IReadOnlyCollection<Menu> allMenus,
        CancellationToken cancellationToken)
    {
        if (dataScopes.Count == 0)
        {
            return;
        }

        var validMenuIds = allMenus.Select(entity => entity.Id).ToHashSet();
        if (dataScopes.Any(entity => !validMenuIds.Contains(entity.MenuId)))
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more data scope menus are invalid.");
        }

        var normalizedScopes = dataScopes
            .Select(entity => new
            {
                entity.ScopeType,
                DepartmentIds = entity.ScopeType == DataScopeType.CustomDepartments
                    ? entity.DepartmentIds.Distinct().OrderBy(id => id).ToArray()
                    : []
            })
            .ToList();
        var firstScope = normalizedScopes.First();
        var hasMultipleScopes = normalizedScopes.Any(entity =>
            entity.ScopeType != firstScope.ScopeType ||
            !entity.DepartmentIds.SequenceEqual(firstScope.DepartmentIds));

        if (hasMultipleScopes)
        {
            throw new BusinessException(
                ErrorCode.BadRequest,
                "Menu-level data scopes are not supported yet. Use one role-level data scope for all selected menus.");
        }

        if (firstScope.ScopeType == DataScopeType.CustomDepartments)
        {
            var validDepartmentCount = _departmentRepository.Query()
                .Count(entity => entity.TenantId == role.TenantId && firstScope.DepartmentIds.Contains(entity.Id));
            if (validDepartmentCount != firstScope.DepartmentIds.Length)
            {
                throw new BusinessException(ErrorCode.BadRequest, "One or more departments are invalid.");
            }
        }

        var dataScope = _roleDataScopeRepository.Query().FirstOrDefault(entity => entity.RoleId == role.Id);
        if (dataScope is null)
        {
            dataScope = new RoleDataScope
            {
                TenantId = role.TenantId,
                RoleId = role.Id
            };
            await _roleDataScopeRepository.AddAsync(dataScope, cancellationToken);
        }

        dataScope.ScopeType = firstScope.ScopeType;
        dataScope.CustomDepartmentIds = firstScope.DepartmentIds.Length > 0
            ? System.Text.Json.JsonSerializer.Serialize(firstScope.DepartmentIds)
            : null;

    }

    private async Task RemoveRolePermissionCachesAsync(Role role, CancellationToken cancellationToken)
    {
        await _cacheService.RemoveAsync(BuildRolePermissionMatrixCacheKey(role.TenantId, role.Id), cancellationToken);

        var userIds = _userRoleRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && entity.RoleId == role.Id)
            .Select(entity => entity.UserId)
            .Distinct()
            .ToArray();

        foreach (var userId in userIds)
        {
            await _cacheService.RemoveAsync(BuildUserMenusCacheKey(role.TenantId, userId), cancellationToken);
            await _cacheService.RemoveAsync(BuildUserPermissionsCacheKey(role.TenantId, userId), cancellationToken);
        }
    }

    private Guid[] GetRoleUserIds(Guid tenantId, Guid roleId)
    {
        return _userRoleRepository.Query()
            .Where(entity => entity.TenantId == tenantId && entity.RoleId == roleId)
            .Select(entity => entity.UserId)
            .Distinct()
            .ToArray();
    }

    private void RotateRoleUserSecurityStamps(Role role)
    {
        RotateUserSecurityStamps(role.TenantId, GetRoleUserIds(role.TenantId, role.Id));
    }

    private void RotateUserSecurityStamps(Guid tenantId, IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        foreach (var user in _userRepository.Query()
                     .Where(entity => entity.TenantId == tenantId && userIds.Contains(entity.Id))
                     .ToList())
        {
            user.RotateSecurityStamp();
        }
    }

    private async Task RemoveRoleUserCachesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        foreach (var userId in userIds.Distinct())
        {
            await _cacheService.RemoveAsync(BuildUserMenusCacheKey(tenantId, userId), cancellationToken);
            await _cacheService.RemoveAsync(BuildUserPermissionsCacheKey(tenantId, userId), cancellationToken);
            await _cacheService.RemoveAsync(BuildUserRolesCacheKey(tenantId, userId), cancellationToken);
        }
    }

    private static string BuildRolePermissionMatrixCacheKey(Guid tenantId, Guid roleId)
    {
        return $"ps:role-permission-matrix:{tenantId}:{roleId}";
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

    private PermissionModuleResponse BuildModule(
        Menu module,
        IReadOnlyDictionary<Guid, List<Menu>> rowsByParentId,
        IReadOnlyCollection<Domain.Entities.Permission> permissions,
        IReadOnlyCollection<Menu> allMenus,
        HashSet<Guid> checkedMenuIds,
        HashSet<Guid> checkedPermissionIds,
        string dataScopeSummary)
    {
        var rowMenus = GetDescendantMenus(module, rowsByParentId).ToList();
        if (rowMenus.Count == 0)
        {
            rowMenus.Add(module);
        }

        var resourceUsage = allMenus
            .Select(menu => GetPermissionResource(menu.PermissionCode))
            .Where(resource => !string.IsNullOrWhiteSpace(resource))
            .GroupBy(resource => resource!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var rows = rowMenus
            .OrderBy(entity => entity.Sort)
            .Select(menu => BuildMenuRow(
                menu,
                permissions,
                resourceUsage,
                checkedMenuIds,
                checkedPermissionIds,
                dataScopeSummary))
            .ToList();
        var totalItemCount = rows.Sum(GetSelectableItemCount);
        var checkedItemCount = rows.Sum(GetCheckedItemCount);

        return new PermissionModuleResponse
        {
            ModuleId = module.Id,
            ModuleName = module.Name,
            ModuleCode = module.PermissionCode,
            Sort = module.Sort,
            Checked = totalItemCount > 0 && checkedItemCount == totalItemCount,
            Indeterminate = checkedItemCount > 0 && checkedItemCount < totalItemCount,
            Expanded = true,
            Menus = rows
        };
    }

    private static IEnumerable<Menu> GetDescendantMenus(Menu parent, IReadOnlyDictionary<Guid, List<Menu>> rowsByParentId)
    {
        if (!rowsByParentId.TryGetValue(parent.Id, out var children))
        {
            yield break;
        }

        foreach (var child in children.OrderBy(entity => entity.Sort))
        {
            yield return child;

            foreach (var descendant in GetDescendantMenus(child, rowsByParentId))
            {
                yield return descendant;
            }
        }
    }

    private static PermissionMenuRowResponse BuildMenuRow(
        Menu menu,
        IReadOnlyCollection<Domain.Entities.Permission> permissions,
        IReadOnlyDictionary<string, int> resourceUsage,
        HashSet<Guid> checkedMenuIds,
        HashSet<Guid> checkedPermissionIds,
        string dataScopeSummary)
    {
        var permissionItems = GetMenuPermissions(menu, permissions, resourceUsage)
            .OrderBy(GetPermissionSort)
            .ThenBy(entity => entity.Code)
            .Select(permission => new PermissionItemResponse
            {
                PermissionId = permission.Id,
                PermissionName = permission.Name,
                PermissionCode = permission.Code,
                PermissionType = string.IsNullOrWhiteSpace(permission.Action) ? "custom" : permission.Action,
                Sort = GetPermissionSort(permission),
                Checked = checkedPermissionIds.Contains(permission.Id)
            })
            .ToList();
        var isMenuChecked = checkedMenuIds.Contains(menu.Id);
        var totalItemCount = 1 + permissionItems.Count;
        var checkedItemCount = (isMenuChecked ? 1 : 0) + permissionItems.Count(entity => entity.Checked);

        return new PermissionMenuRowResponse
        {
            MenuId = menu.Id,
            ParentId = menu.ParentId,
            MenuName = menu.Name,
            MenuPath = menu.Path,
            MenuCode = menu.PermissionCode,
            Icon = menu.Icon,
            Sort = menu.Sort,
            Checked = isMenuChecked,
            Indeterminate = checkedItemCount > 0 && checkedItemCount < totalItemCount,
            Permissions = permissionItems,
            DataScopeEnabled = true,
            FieldPermissionEnabled = false,
            DataScopeSummary = dataScopeSummary,
            FieldPermissionSummary = null
        };
    }

    private static IEnumerable<Domain.Entities.Permission> GetMenuPermissions(
        Menu menu,
        IReadOnlyCollection<Domain.Entities.Permission> permissions,
        IReadOnlyDictionary<string, int> resourceUsage)
    {
        var menuResource = GetPermissionResource(menu.PermissionCode);
        var exactPermissionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(menu.PermissionCode))
        {
            exactPermissionCodes.Add(menu.PermissionCode);
        }

        foreach (var permission in permissions)
        {
            if (exactPermissionCodes.Contains(permission.Code))
            {
                yield return permission;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(menuResource) &&
                !string.IsNullOrWhiteSpace(permission.Resource) &&
                resourceUsage.TryGetValue(menuResource, out var usageCount) &&
                usageCount == 1 &&
                string.Equals(permission.Resource, menuResource, StringComparison.OrdinalIgnoreCase))
            {
                yield return permission;
            }
        }
    }

    private static Menu? ResolveMenuForPermission(
        Domain.Entities.Permission permission,
        IReadOnlyCollection<Menu> allMenus,
        IReadOnlyDictionary<string, int> resourceUsage)
    {
        var exactMenu = allMenus
            .Where(menu => !string.IsNullOrWhiteSpace(menu.PermissionCode) &&
                string.Equals(menu.PermissionCode, permission.Code, StringComparison.OrdinalIgnoreCase))
            .OrderBy(menu => menu.Sort)
            .FirstOrDefault();
        if (exactMenu is not null)
        {
            return exactMenu;
        }

        if (string.IsNullOrWhiteSpace(permission.Resource) ||
            !resourceUsage.TryGetValue(permission.Resource, out var usageCount) ||
            usageCount != 1)
        {
            return null;
        }

        return allMenus
            .Where(menu => string.Equals(
                GetPermissionResource(menu.PermissionCode),
                permission.Resource,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(menu => menu.Sort)
            .FirstOrDefault();
    }

    private static void AddMenuAndAncestors(
        Menu menu,
        IReadOnlyDictionary<Guid, Menu> menusById,
        HashSet<Guid> resolvedMenuIds)
    {
        resolvedMenuIds.Add(menu.Id);

        var parentId = menu.ParentId;
        while (parentId.HasValue && menusById.TryGetValue(parentId.Value, out var parent))
        {
            if (!resolvedMenuIds.Add(parent.Id))
            {
                break;
            }

            parentId = parent.ParentId;
        }
    }

    private string BuildDataScopeSummary(Role role)
    {
        var dataScope = _roleDataScopeRepository.Query().FirstOrDefault(entity => entity.RoleId == role.Id);
        var scopeType = dataScope?.ScopeType ?? GetDefaultScopeType(role);
        return scopeType.ToString();
    }

    private static DataScopeType GetDefaultScopeType(Role role)
    {
        return string.Equals(role.Code, ClaimConstants.SuperAdminRoleCode, StringComparison.OrdinalIgnoreCase)
            ? DataScopeType.All
            : DataScopeType.CurrentUser;
    }

    private static string? GetPermissionResource(string? permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            return null;
        }

        var segments = permissionCode.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length >= 2
            ? string.Join(':', segments.Take(2))
            : permissionCode.Trim();
    }

    private static int GetSelectableItemCount(PermissionMenuRowResponse row)
    {
        return 1 + row.Permissions.Count;
    }

    private static int GetCheckedItemCount(PermissionMenuRowResponse row)
    {
        return (row.Checked ? 1 : 0) + row.Permissions.Count(entity => entity.Checked);
    }

    private static int GetPermissionSort(Domain.Entities.Permission permission)
    {
        return permission.Action?.ToLowerInvariant() switch
        {
            "view" => 10,
            "create" => 20,
            "update" => 30,
            "delete" => 40,
            "import" => 50,
            "export" => 60,
            "upload" => 70,
            "download" => 80,
            "trigger" => 90,
            "data-scope" => 100,
            "permission-matrix" => 110,
            "assign-permission" => 120,
            "assign-user" => 130,
            "kickout" => 140,
            _ => 1000
        };
    }
}
