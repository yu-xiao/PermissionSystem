using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Tenants;

public sealed class TenantInitializationJob
{
    private static readonly PermissionSeed[] PermissionSeeds =
    [
        new("system:user:view", "查看用户", "system:user", "view"),
        new("system:user:create", "新增用户", "system:user", "create"),
        new("system:user:update", "编辑用户", "system:user", "update"),
        new("system:user:delete", "删除用户", "system:user", "delete"),
        new("system:role:view", "查看角色", "system:role", "view"),
        new("system:role:create", "新增角色", "system:role", "create"),
        new("system:role:update", "编辑角色", "system:role", "update"),
        new("system:role:delete", "删除角色", "system:role", "delete"),
        new("system:role:permission-matrix", "角色权限矩阵", "system:role", "permission-matrix"),
        new("system:role:assign-permission", "分配角色权限", "system:role", "assign-permission"),
        new("system:role:assign-user", "关联角色用户", "system:role", "assign-user"),
        new("system:role:data-scope", "配置角色数据范围", "system:role", "data-scope"),
        new("system:department:view", "查看部门", "system:department", "view"),
        new("system:department:create", "新增部门", "system:department", "create"),
        new("system:department:update", "编辑部门", "system:department", "update"),
        new("system:department:delete", "删除部门", "system:department", "delete"),
        new("system:menu:view", "查看菜单", "system:menu", "view"),
        new("system:menu:create", "新增菜单", "system:menu", "create"),
        new("system:menu:update", "编辑菜单", "system:menu", "update"),
        new("system:menu:delete", "删除菜单", "system:menu", "delete"),
        new("system:permission:view", "查看权限", "system:permission", "view"),
        new("system:permission:create", "新增权限", "system:permission", "create"),
        new("system:permission:update", "编辑权限", "system:permission", "update"),
        new("system:permission:delete", "删除权限", "system:permission", "delete"),
        new("system:config:view", "查看系统配置", "system:config", "view"),
        new("system:config:create", "新增系统配置", "system:config", "create"),
        new("system:config:update", "编辑系统配置", "system:config", "update"),
        new("system:config:delete", "删除系统配置", "system:config", "delete"),
        new("security:policy:view", "查看安全策略", "security:policy", "view"),
        new("security:policy:update", "修改安全策略", "security:policy", "update"),
        new(AiCenterConstants.ChatUsePermission, "使用 AI 问答", "ai:chat", "use"),
        new(AiCenterConstants.ConversationViewPermission, "查看本人 AI 会话", "ai:conversation", "view"),
        new(AiCenterConstants.ToolQueryPermission, "调用 AI 只读工具", "ai:tool", "query"),
        new(AiCenterConstants.UserQueryPermission, "AI 查询用户摘要", "ai:tool", "user-query"),
        new(AiCenterConstants.DepartmentQueryPermission, "AI 查询部门摘要", "ai:tool", "department-query"),
        new(AiCenterConstants.RoleQueryPermission, "AI 查询角色摘要", "ai:tool", "role-query"),
        new(AiCenterConstants.LoginLogQueryPermission, "AI 查询登录统计", "ai:tool", "login-log-query"),
        new(AiCenterConstants.OperationLogQueryPermission, "AI 查询操作统计", "ai:tool", "operation-log-query"),
        new(AiCenterConstants.ReportDatasetQueryPermission, "AI 查询批准数据集", "ai:tool", "dataset-query"),
        new(AiCenterConstants.ProviderViewPermission, "查看 AI 模型配置", "ai:provider", "view"),
        new(AiCenterConstants.ProviderCreatePermission, "新增 AI 模型配置", "ai:provider", "create"),
        new(AiCenterConstants.ProviderUpdatePermission, "修改 AI 模型配置", "ai:provider", "update"),
        new(AiCenterConstants.ProviderDeletePermission, "删除 AI 模型配置", "ai:provider", "delete"),
        new(AiCenterConstants.ProviderTestPermission, "测试 AI 模型连接", "ai:provider", "test"),
        new(AiCenterConstants.ProviderCompliancePermission, "确认 AI 模型合规", "ai:provider", "compliance"),
        new(AiCenterConstants.McpClientViewPermission, "查看 MCP 客户端", "ai:mcp-client", "view"),
        new(AiCenterConstants.McpClientManagePermission, "管理 MCP 客户端", "ai:mcp-client", "manage"),
        new(AiCenterConstants.McpClientSecretPermission, "轮换 MCP 客户端密钥", "ai:mcp-client", "secret"),
        new(AiCenterConstants.McpAuditViewPermission, "查看 MCP 调用审计", "ai:mcp-audit", "view"),
        new(AiCenterConstants.GovernanceViewPermission, "查看 AI 模型治理", "ai:governance", "view"),
        new(AiCenterConstants.GovernanceManagePermission, "管理 AI 模型路由和预算", "ai:governance", "manage"),
        new(AiCenterConstants.OperationsViewPermission, "查看 AI 运营数据", "ai:operations", "view")
    ];

    private static readonly MenuSeed[] MenuSeeds =
    [
        new("system", null, "系统管理", "/system", "Layout", "Setting", 1, "Directory", null),
        new("users", "system", "用户管理", "/system/users", "system/user/index", "User", 1, "Menu", "system:user:view"),
        new("roles", "system", "角色管理", "/system/roles", "system/role/index", "UserFilled", 2, "Menu", "system:role:view"),
        new("departments", "system", "部门管理", "/system/departments", "system/department/index", "OfficeBuilding", 3, "Menu", "system:department:view"),
        new("menus", "system", "菜单管理", "/system/menus", "system/menu/index", "Menu", 4, "Menu", "system:menu:view"),
        new("permissions", "system", "权限管理", "/system/permissions", "system/permission/index", "Key", 5, "Menu", "system:permission:view"),
        new("configs", "system", "系统配置", "/system/configs", "system/config/index", "Tools", 6, "Menu", "system:config:view"),
        new("security-policy", "system", "安全策略", "/security/policy", "security/policy/index", "Lock", 7, "Menu", "security:policy:view"),
        new("ai-providers", "system", "AI 模型配置", "/system/ai-providers", "ai/provider/index", "ChatDotRound", 8, "Menu", AiCenterConstants.ProviderViewPermission),
        new("ai-mcp-clients", "system", "MCP 客户端", "/system/ai-mcp-clients", "ai/mcp-client/index", "Connection", 9, "Menu", AiCenterConstants.McpClientViewPermission),
        new("ai-mcp-audit", "system", "MCP 调用审计", "/system/ai-mcp-audit", "ai/mcp-audit/index", "DocumentChecked", 10, "Menu", AiCenterConstants.McpAuditViewPermission),
        new("ai-governance", "system", "AI 模型治理", "/system/ai-governance", "ai/governance/index", "SetUp", 11, "Menu", AiCenterConstants.GovernanceViewPermission),
        new("ai-operations", "system", "AI 运营中心", "/system/ai-operations", "ai/operations/index", "DataAnalysis", 12, "Menu", AiCenterConstants.OperationsViewPermission)
    ];

    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<Permission> _permissionRepository;
    private readonly IRepository<Menu> _menuRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<RolePermission> _rolePermissionRepository;
    private readonly IRepository<RoleMenu> _roleMenuRepository;
    private readonly IRepository<RoleDataScope> _roleDataScopeRepository;
    private readonly IRepository<SecurityPolicy> _securityPolicyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedLock _distributedLock;
    private readonly ISystemTenantScope _systemTenantScope;
    private readonly IMcpDatasetProvisioner _mcpDatasetProvisioner;
    private readonly ILogger<TenantInitializationJob> _logger;

    public TenantInitializationJob(
        IRepository<Tenant> tenantRepository,
        IRepository<Department> departmentRepository,
        IRepository<User> userRepository,
        IRepository<Role> roleRepository,
        IRepository<Permission> permissionRepository,
        IRepository<Menu> menuRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<RolePermission> rolePermissionRepository,
        IRepository<RoleMenu> roleMenuRepository,
        IRepository<RoleDataScope> roleDataScopeRepository,
        IRepository<SecurityPolicy> securityPolicyRepository,
        IUnitOfWork unitOfWork,
        IDistributedLock distributedLock,
        ISystemTenantScope systemTenantScope,
        IMcpDatasetProvisioner mcpDatasetProvisioner,
        ILogger<TenantInitializationJob> logger)
    {
        _tenantRepository = tenantRepository;
        _departmentRepository = departmentRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _menuRepository = menuRepository;
        _userRoleRepository = userRoleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _roleMenuRepository = roleMenuRepository;
        _roleDataScopeRepository = roleDataScopeRepository;
        _securityPolicyRepository = securityPolicyRepository;
        _unitOfWork = unitOfWork;
        _distributedLock = distributedLock;
        _systemTenantScope = systemTenantScope;
        _mcpDatasetProvisioner = mcpDatasetProvisioner;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid tenantId)
    {
        using var systemScope = _systemTenantScope.Begin(SystemTenantOperations.TenantInitialization);
        await _distributedLock.ExecuteWithLockAsync(
            $"tenant:initialize:{tenantId:N}",
            token => InitializeAsync(tenantId, token),
            TimeSpan.FromMinutes(5),
            TimeSpan.Zero);
    }

    private async Task InitializeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = GetTenant(tenantId);
        if (tenant.Status is TenantStatus.Active or TenantStatus.Disabled or TenantStatus.Archived)
        {
            return;
        }

        try
        {
            tenant.Status = TenantStatus.Initializing;
            tenant.StatusChangedAt = DateTimeOffset.UtcNow;
            tenant.InitializationAttempts++;
            tenant.InitializationStartedAt = DateTimeOffset.UtcNow;
            tenant.InitializationError = null;
            await SetProgressAsync(tenant, "Department", 10, cancellationToken);

            var department = await EnsureDepartmentAsync(tenantId, cancellationToken);
            await SetProgressAsync(tenant, "Role", 25, cancellationToken);
            var role = await EnsureRoleAsync(tenantId, cancellationToken);
            await EnsureRoleDataScopeAsync(tenantId, role.Id, cancellationToken);

            await SetProgressAsync(tenant, "Permissions", 40, cancellationToken);
            var permissions = await EnsurePermissionsAsync(tenantId, cancellationToken);
            await SetProgressAsync(tenant, "Menus", 60, cancellationToken);
            var menus = await EnsureMenusAsync(tenantId, cancellationToken);

            await SetProgressAsync(tenant, "Administrator", 75, cancellationToken);
            var administrator = _userRepository.QueryForTenant(tenantId)
                .OrderBy(entity => entity.CreatedAt)
                .FirstOrDefault(entity => entity.IsBuiltin)
                ?? throw new BusinessException(ErrorCode.NotFound, "Tenant bootstrap administrator was not found.");
            administrator.DepartmentId = department.Id;
            _userRepository.Update(administrator);
            if (!_userRoleRepository.QueryForTenant(tenantId).Any(entity => entity.UserId == administrator.Id && entity.RoleId == role.Id))
            {
                await _userRoleRepository.AddAsync(new UserRole { TenantId = tenantId, UserId = administrator.Id, RoleId = role.Id }, cancellationToken);
            }

            await SetProgressAsync(tenant, "RoleRelations", 85, cancellationToken);
            foreach (var permission in permissions)
            {
                if (!_rolePermissionRepository.QueryForTenant(tenantId).Any(entity => entity.RoleId == role.Id && entity.PermissionId == permission.Id))
                {
                    await _rolePermissionRepository.AddAsync(new RolePermission { TenantId = tenantId, RoleId = role.Id, PermissionId = permission.Id }, cancellationToken);
                }
            }
            foreach (var menu in menus)
            {
                if (!_roleMenuRepository.QueryForTenant(tenantId).Any(entity => entity.RoleId == role.Id && entity.MenuId == menu.Id))
                {
                    await _roleMenuRepository.AddAsync(new RoleMenu { TenantId = tenantId, RoleId = role.Id, MenuId = menu.Id }, cancellationToken);
                }
            }

            await SetProgressAsync(tenant, "SecurityPolicy", 95, cancellationToken);
            await EnsureSecurityPolicyAsync(tenantId, cancellationToken);

            await SetProgressAsync(tenant, "McpDatasets", 98, cancellationToken);
            await _mcpDatasetProvisioner.EnsureTenantDatasetsAsync(tenantId, cancellationToken);

            tenant.Status = TenantStatus.Active;
            tenant.StatusChangedAt = DateTimeOffset.UtcNow;
            tenant.InitializationStep = "Completed";
            tenant.InitializationProgress = 100;
            tenant.InitializedAt = DateTimeOffset.UtcNow;
            tenant.InitializationError = null;
            _tenantRepository.Update(tenant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Tenant initialization completed. TenantId: {TenantId}", tenantId);
        }
        catch (Exception exception)
        {
            tenant.Status = TenantStatus.Failed;
            tenant.StatusChangedAt = DateTimeOffset.UtcNow;
            tenant.InitializationError = Truncate(exception.Message, 2000);
            _tenantRepository.Update(tenant);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            _logger.LogError(exception, "Tenant initialization failed. TenantId: {TenantId}, Step: {Step}", tenantId, tenant.InitializationStep);
            throw;
        }
    }

    private Tenant GetTenant(Guid tenantId) => _tenantRepository.QueryForTenant(tenantId)
        .FirstOrDefault(entity => entity.Id == tenantId)
        ?? throw new BusinessException(ErrorCode.NotFound, "Tenant was not found.");

    private async Task SetProgressAsync(Tenant tenant, string step, int progress, CancellationToken cancellationToken)
    {
        tenant.InitializationStep = step;
        tenant.InitializationProgress = progress;
        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Department> EnsureDepartmentAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var department = _departmentRepository.QueryForTenant(tenantId).FirstOrDefault(entity => entity.Code == "root");
        if (department is not null) return department;
        department = new Department { TenantId = tenantId, Code = "root", Name = "根部门", Sort = 1, Status = "Enabled", IsEnabled = true };
        department.Id = Guid.NewGuid();
        department.TreePath = $"/{department.Id}/";
        await _departmentRepository.AddAsync(department, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return department;
    }

    private async Task<Role> EnsureRoleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var role = _roleRepository.QueryForTenant(tenantId).FirstOrDefault(entity => entity.Code == SystemBuiltinConstants.TenantAdminRoleCode);
        if (role is not null) return role;
        role = new Role { TenantId = tenantId, Code = SystemBuiltinConstants.TenantAdminRoleCode, Name = SystemBuiltinConstants.TenantAdminRoleName, Description = "系统内置租户管理员角色。", IsEnabled = true, IsBuiltin = true, Sort = 1 };
        await _roleRepository.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return role;
    }

    private async Task EnsureRoleDataScopeAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        if (_roleDataScopeRepository.QueryForTenant(tenantId).Any(entity => entity.RoleId == roleId)) return;
        await _roleDataScopeRepository.AddAsync(new RoleDataScope { TenantId = tenantId, RoleId = roleId, ScopeType = DataScopeType.All }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Permission>> EnsurePermissionsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var result = new List<Permission>();
        foreach (var seed in PermissionSeeds)
        {
            var permission = _permissionRepository.QueryForTenant(tenantId).FirstOrDefault(entity => entity.Code == seed.Code);
            if (permission is null)
            {
                permission = new Permission { TenantId = tenantId, Code = seed.Code, Name = seed.Name, Group = "系统管理", Resource = seed.Resource, Action = seed.Action };
                await _permissionRepository.AddAsync(permission, cancellationToken);
            }
            result.Add(permission);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<IReadOnlyList<Menu>> EnsureMenusAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var result = new List<Menu>();
        var byKey = new Dictionary<string, Menu>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in MenuSeeds)
        {
            var menu = _menuRepository.QueryForTenant(tenantId).FirstOrDefault(entity => entity.Path == seed.Path);
            if (menu is null)
            {
                menu = new Menu { TenantId = tenantId, ParentId = seed.ParentKey is null ? null : byKey[seed.ParentKey].Id, Name = seed.Name, Path = seed.Path, Component = seed.Component, Icon = seed.Icon, Sort = seed.Sort, Visible = true, MenuType = seed.MenuType, PermissionCode = seed.PermissionCode };
                await _menuRepository.AddAsync(menu, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            byKey[seed.Key] = menu;
            result.Add(menu);
        }
        return result;
    }

    private async Task EnsureSecurityPolicyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_securityPolicyRepository.QueryForTenant(tenantId).Any()) return;
        await _securityPolicyRepository.AddAsync(new SecurityPolicy { TenantId = tenantId }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private sealed record PermissionSeed(string Code, string Name, string Resource, string Action);
    private sealed record MenuSeed(string Key, string? ParentKey, string Name, string Path, string Component, string Icon, int Sort, string MenuType, string? PermissionCode);
}
