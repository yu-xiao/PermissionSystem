using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PermissionSystem.Infrastructure.SeedData;

public sealed class SeedDataInitializer
{
    private static readonly Guid DefaultTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SuperAdminRoleId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid SystemManagementMenuId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid ScheduledTaskMenuId = Guid.Parse("40000000-0000-0000-0000-000000000006");
    private static readonly Guid DemoScheduledTaskId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ILogger<SeedDataInitializer> _logger;

    public SeedDataInitializer(
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IOpenIddictApplicationManager applicationManager,
        ILogger<SeedDataInitializer> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _applicationManager = applicationManager;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await SeedTenantAsync(cancellationToken);
        await SeedRoleAsync(cancellationToken);
        await SeedAdminUserAsync(cancellationToken);
        await SeedPermissionsAsync(cancellationToken);
        await SeedMenusAsync(cancellationToken);
        await SeedScheduledTasksAsync(cancellationToken);
        await SeedRoleRelationsAsync(cancellationToken);
        await SeedOAuthClientAsync(cancellationToken);

        _logger.LogInformation("Development seed data initialization completed.");
    }

    private async Task SeedTenantAsync(CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(entity => entity.Code == "default", cancellationToken);
        if (tenant is not null)
        {
            tenant.Name = "默认租户";
            tenant.Description = "本地开发默认租户。";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _dbContext.Tenants.Add(new Tenant
        {
            Id = DefaultTenantId,
            TenantId = DefaultTenantId,
            Code = "default",
            Name = "默认租户",
            Description = "本地开发默认租户。",
            IsEnabled = true
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRoleAsync(CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.Code == "SuperAdmin",
            cancellationToken);
        if (role is not null)
        {
            role.Name = "超级管理员";
            role.Description = "系统内置超级管理员角色。";
            role.IsEnabled = true;
            role.Sort = 1;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _dbContext.Roles.Add(new Role
        {
            Id = SuperAdminRoleId,
            TenantId = DefaultTenantId,
            Code = "SuperAdmin",
            Name = "超级管理员",
            Description = "系统内置超级管理员角色。",
            IsEnabled = true,
            Sort = 1
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken)
    {
        var admin = await _dbContext.Users
            .FirstOrDefaultAsync(entity => entity.TenantId == DefaultTenantId && entity.NormalizedUserName == "ADMIN", cancellationToken);

        if (admin is null)
        {
            admin = new User
            {
                Id = AdminUserId,
                TenantId = DefaultTenantId,
                UserName = "admin",
                NormalizedUserName = "ADMIN",
                DisplayName = "系统管理员",
                IsEnabled = true
            };

            admin.PasswordHash = _passwordHasher.HashPassword(admin, "admin123456");
            _dbContext.Users.Add(admin);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (admin.DisplayName == "System Administrator")
        {
            admin.DisplayName = "系统管理员";
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await _dbContext.UserRoles.AnyAsync(
            entity => entity.TenantId == DefaultTenantId && entity.UserId == admin.Id && entity.RoleId == SuperAdminRoleId,
            cancellationToken))
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                TenantId = DefaultTenantId,
                UserId = admin.Id,
                RoleId = SuperAdminRoleId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var permissions = new[]
        {
            ("system:user:view", "查看用户", "system:user", "view"),
            ("system:user:create", "新增用户", "system:user", "create"),
            ("system:user:update", "编辑用户", "system:user", "update"),
            ("system:user:delete", "删除用户", "system:user", "delete"),
            ("system:role:view", "查看角色", "system:role", "view"),
            ("system:role:create", "新增角色", "system:role", "create"),
            ("system:role:update", "编辑角色", "system:role", "update"),
            ("system:role:delete", "删除角色", "system:role", "delete"),
            ("system:menu:view", "查看菜单", "system:menu", "view"),
            ("system:menu:create", "新增菜单", "system:menu", "create"),
            ("system:menu:update", "编辑菜单", "system:menu", "update"),
            ("system:menu:delete", "删除菜单", "system:menu", "delete"),
            ("system:permission:view", "查看权限", "system:permission", "view"),
            ("system:permission:create", "新增权限", "system:permission", "create"),
            ("system:permission:update", "编辑权限", "system:permission", "update"),
            ("system:permission:delete", "删除权限", "system:permission", "delete"),
            ("system:scheduled-task:view", "查看定时任务", "system:scheduled-task", "view"),
            ("system:scheduled-task:create", "新增定时任务", "system:scheduled-task", "create"),
            ("system:scheduled-task:update", "编辑定时任务", "system:scheduled-task", "update"),
            ("system:scheduled-task:delete", "删除定时任务", "system:scheduled-task", "delete"),
            ("system:scheduled-task:trigger", "触发定时任务", "system:scheduled-task", "trigger")
        };

        foreach (var (code, name, resource, action) in permissions)
        {
            var existingPermission = await _dbContext.Permissions.FirstOrDefaultAsync(
                entity => entity.TenantId == DefaultTenantId && entity.Code == code,
                cancellationToken);
            if (existingPermission is not null)
            {
                existingPermission.Name = name;
                existingPermission.Group = "系统管理";
                existingPermission.Resource = resource;
                existingPermission.Action = action;
                continue;
            }

            _dbContext.Permissions.Add(new Permission
            {
                TenantId = DefaultTenantId,
                Code = code,
                Name = name,
                Group = "系统管理",
                Resource = resource,
                Action = action
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedMenusAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(
            SystemManagementMenuId,
            null,
            "系统管理",
            "/system",
            "Layout",
            null,
            "Setting",
            1,
            "Directory",
            null,
            cancellationToken);

        await EnsureMenuAsync(
            Guid.Parse("40000000-0000-0000-0000-000000000002"),
            SystemManagementMenuId,
            "用户管理",
            "/system/users",
            "system/users/index",
            null,
            "User",
            10,
            "Menu",
            "system:user:view",
            cancellationToken);

        await EnsureMenuAsync(
            Guid.Parse("40000000-0000-0000-0000-000000000003"),
            SystemManagementMenuId,
            "角色管理",
            "/system/roles",
            "system/roles/index",
            null,
            "UserFilled",
            20,
            "Menu",
            "system:role:view",
            cancellationToken);

        await EnsureMenuAsync(
            Guid.Parse("40000000-0000-0000-0000-000000000004"),
            SystemManagementMenuId,
            "菜单管理",
            "/system/menus",
            "system/menus/index",
            null,
            "Menu",
            30,
            "Menu",
            "system:menu:view",
            cancellationToken);

        await EnsureMenuAsync(
            Guid.Parse("40000000-0000-0000-0000-000000000005"),
            SystemManagementMenuId,
            "权限管理",
            "/system/permissions",
            "system/permissions/index",
            null,
            "Lock",
            40,
            "Menu",
            "system:permission:view",
            cancellationToken);

        await EnsureMenuAsync(
            ScheduledTaskMenuId,
            SystemManagementMenuId,
            "定时任务",
            "/system/scheduled-tasks",
            "system/scheduled-task/index",
            null,
            "Timer",
            50,
            "Menu",
            "system:scheduled-task:view",
            cancellationToken);
    }

    private async Task SeedScheduledTasksAsync(CancellationToken cancellationToken)
    {
        var existingTask = await _dbContext.ScheduledTasks.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.Code == "demo-minute-log",
            cancellationToken);

        if (existingTask is not null)
        {
            existingTask.Name = "Demo minute log task";
            existingTask.JobType = "DemoLog";
            existingTask.CronExpression = "* * * * *";
            existingTask.Queue = "default";
            existingTask.Description = "Demo task for testing frontend-configured Hangfire recurring execution.";
            existingTask.ParametersJson = "{\"source\":\"seed-demo\"}";
            existingTask.IsEnabled = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _dbContext.ScheduledTasks.Add(new ScheduledTask
        {
            Id = DemoScheduledTaskId,
            TenantId = DefaultTenantId,
            Code = "demo-minute-log",
            Name = "Demo minute log task",
            JobType = "DemoLog",
            CronExpression = "* * * * *",
            Queue = "default",
            Description = "Demo task for testing frontend-configured Hangfire recurring execution.",
            ParametersJson = "{\"source\":\"seed-demo\"}",
            IsEnabled = true
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMenuAsync(
        Guid id,
        Guid? parentId,
        string name,
        string path,
        string component,
        string? redirect,
        string? icon,
        int sort,
        string menuType,
        string? permissionCode,
        CancellationToken cancellationToken)
    {
        var existingMenu = await _dbContext.Menus.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.Id == id,
            cancellationToken);
        if (existingMenu is not null)
        {
            existingMenu.ParentId = parentId;
            existingMenu.Name = name;
            existingMenu.Path = path;
            existingMenu.Component = component;
            existingMenu.Redirect = redirect;
            existingMenu.Icon = icon;
            existingMenu.Sort = sort;
            existingMenu.Visible = true;
            existingMenu.KeepAlive = false;
            existingMenu.MenuType = menuType;
            existingMenu.PermissionCode = permissionCode;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _dbContext.Menus.Add(new Menu
        {
            Id = id,
            TenantId = DefaultTenantId,
            ParentId = parentId,
            Name = name,
            Path = path,
            Component = component,
            Redirect = redirect,
            Icon = icon,
            Sort = sort,
            Visible = true,
            KeepAlive = false,
            MenuType = menuType,
            PermissionCode = permissionCode
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRoleRelationsAsync(CancellationToken cancellationToken)
    {
        var menuIds = await _dbContext.Menus
            .Where(entity => entity.TenantId == DefaultTenantId)
            .Select(entity => entity.Id)
            .ToListAsync(cancellationToken);

        foreach (var menuId in menuIds)
        {
            if (!await _dbContext.RoleMenus.AnyAsync(
                entity => entity.TenantId == DefaultTenantId && entity.RoleId == SuperAdminRoleId && entity.MenuId == menuId,
                cancellationToken))
            {
                _dbContext.RoleMenus.Add(new RoleMenu
                {
                    TenantId = DefaultTenantId,
                    RoleId = SuperAdminRoleId,
                    MenuId = menuId
                });
            }
        }

        var permissionIds = await _dbContext.Permissions
            .Where(entity => entity.TenantId == DefaultTenantId)
            .Select(entity => entity.Id)
            .ToListAsync(cancellationToken);

        foreach (var permissionId in permissionIds)
        {
            if (!await _dbContext.RolePermissions.AnyAsync(
                entity => entity.TenantId == DefaultTenantId && entity.RoleId == SuperAdminRoleId && entity.PermissionId == permissionId,
                cancellationToken))
            {
                _dbContext.RolePermissions.Add(new RolePermission
                {
                    TenantId = DefaultTenantId,
                    RoleId = SuperAdminRoleId,
                    PermissionId = permissionId
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedOAuthClientAsync(CancellationToken cancellationToken)
    {
        const string clientId = "permission-admin";

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Confidential,
            ClientSecret = "permission-admin-secret",
            DisplayName = "Permission Admin",
            RedirectUris =
            {
                new Uri("http://localhost:5173/callback"),
                new Uri("http://localhost:8080/callback")
            },
            PostLogoutRedirectUris =
            {
                new Uri("http://localhost:5173"),
                new Uri("http://localhost:8080")
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.Password,
                Permissions.GrantTypes.RefreshToken,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + Scopes.OpenId,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                Permissions.Prefixes.Scope + "permission-system-api"
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange
            }
        };

        var application = await _applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            await _applicationManager.CreateAsync(descriptor, cancellationToken);
            return;
        }

        await _applicationManager.UpdateAsync(application, descriptor, cancellationToken);
    }
}
