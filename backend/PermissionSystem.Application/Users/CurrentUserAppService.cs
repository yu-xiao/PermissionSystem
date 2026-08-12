using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Menus;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;

namespace PermissionSystem.Application.Users;

public sealed class CurrentUserAppService : ICurrentUserAppService
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IRepository<Menu> _menuRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<RoleMenu> _roleMenuRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<RolePermission> _rolePermissionRepository;
    private readonly IRepository<Domain.Entities.Permission> _permissionRepository;
    private readonly ITenantContext _tenantContext;

    public CurrentUserAppService(
        ICurrentUserService currentUserService,
        IRepository<Menu> menuRepository,
        IRepository<Role> roleRepository,
        IRepository<RoleMenu> roleMenuRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<RolePermission> rolePermissionRepository,
        IRepository<Domain.Entities.Permission> permissionRepository,
        ITenantContext tenantContext)
    {
        _currentUserService = currentUserService;
        _menuRepository = menuRepository;
        _roleRepository = roleRepository;
        _roleMenuRepository = roleMenuRepository;
        _userRoleRepository = userRoleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _permissionRepository = permissionRepository;
        _tenantContext = tenantContext;
    }

    public Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CurrentUserResponse
        {
            UserId = _currentUserService.UserId,
            TenantId = _currentUserService.TenantId,
            Username = _currentUserService.Username,
            IsSuperAdmin = _currentUserService.IsSuperAdmin,
            Roles = _currentUserService.Roles,
            PermissionCodes = _currentUserService.PermissionCodes
        });
    }

    public Task<IReadOnlyList<MenuTreeResponse>> GetCurrentUserMenusAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveEffectiveTenantId();
        if (!tenantId.HasValue)
        {
            return Task.FromResult<IReadOnlyList<MenuTreeResponse>>([]);
        }

        var menus = _currentUserService.IsSuperAdmin
            ? _menuRepository.Query()
                .Where(entity => entity.TenantId == tenantId.Value && entity.Visible)
                .OrderBy(entity => entity.Sort)
                .ToList()
            : GetAssignedMenus(tenantId.Value);

        return Task.FromResult(MenuService.BuildTree(menus));
    }

    public Task<IReadOnlyCollection<string>> GetCurrentUserPermissionCodesAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            var tenantId = ResolveEffectiveTenantId();
            var allPermissions = tenantId.HasValue
                ? _permissionRepository.Query()
                    .Where(entity => entity.TenantId == tenantId.Value)
                    .Select(entity => entity.Code)
                    .ToArray()
                : _currentUserService.PermissionCodes;

            return Task.FromResult<IReadOnlyCollection<string>>(allPermissions);
        }

        return Task.FromResult(_currentUserService.PermissionCodes);
    }

    private Guid? ResolveEffectiveTenantId()
    {
        return _currentUserService.IsSuperAdmin
            ? _tenantContext.TenantId ?? _currentUserService.TenantId
            : _currentUserService.TenantId;
    }

    private List<Menu> GetAssignedMenus(Guid tenantId)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return [];
        }

        var assignedRoleIds = _userRoleRepository.Query()
            .Where(entity => entity.TenantId == tenantId && entity.UserId == userId.Value)
            .Select(entity => entity.RoleId)
            .ToArray();
        var roleIds = _roleRepository.Query()
            .Where(entity => entity.TenantId == tenantId && assignedRoleIds.Contains(entity.Id) && entity.IsEnabled)
            .Select(entity => entity.Id)
            .ToArray();

        var menuIds = _roleMenuRepository.Query()
            .Where(entity => entity.TenantId == tenantId && roleIds.Contains(entity.RoleId))
            .Select(entity => entity.MenuId)
            .Distinct()
            .ToArray();

        var allMenus = _menuRepository.Query()
            .Where(entity => entity.TenantId == tenantId && entity.Visible)
            .OrderBy(entity => entity.Sort)
            .ToList();

        var allowedMenuIds = new HashSet<Guid>(menuIds);
        var expandedMenuIds = new HashSet<Guid>(allowedMenuIds);

        foreach (var menu in allMenus.Where(entity => allowedMenuIds.Contains(entity.Id)))
        {
            var parentId = menu.ParentId;
            while (parentId.HasValue)
            {
                var parent = allMenus.FirstOrDefault(entity => entity.Id == parentId.Value);
                if (parent is null || !expandedMenuIds.Add(parent.Id))
                {
                    break;
                }

                parentId = parent.ParentId;
            }
        }

        return allMenus
            .Where(entity => expandedMenuIds.Contains(entity.Id))
            .OrderBy(entity => entity.Sort)
            .ToList();
    }
}
