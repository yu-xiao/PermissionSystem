using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PermissionSystem.Infrastructure.SeedData;

public sealed class SeedDataInitializer
{
    private static readonly Guid DefaultTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SuperAdminRoleId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid DefaultDepartmentId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid SystemManagementMenuId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantMenuId = Guid.Parse("40000000-0000-0000-0000-000000000009");
    private static readonly Guid DepartmentMenuId = Guid.Parse("40000000-0000-0000-0000-00000000000A");
    private static readonly Guid DictionaryMenuId = Guid.Parse("40000000-0000-0000-0000-00000000000B");
    private static readonly Guid SystemConfigMenuId = Guid.Parse("40000000-0000-0000-0000-00000000000C");
    private static readonly Guid FileMenuId = Guid.Parse("40000000-0000-0000-0000-00000000000D");
    private static readonly Guid OutboxMessageMenuId = Guid.Parse("40000000-0000-0000-0000-00000000000E");
    private static readonly Guid InboxMessageMenuId = Guid.Parse("40000000-0000-0000-0000-00000000000F");
    private static readonly Guid HealthMenuId = Guid.Parse("40000000-0000-0000-0000-000000000010");
    private static readonly Guid JobMenuId = Guid.Parse("40000000-0000-0000-0000-000000000011");
    private static readonly Guid NotificationMenuId = Guid.Parse("40000000-0000-0000-0000-000000000012");
    private static readonly Guid NotificationAdminMenuId = Guid.Parse("40000000-0000-0000-0000-000000000013");
    private static readonly Guid OnlineUserMenuId = Guid.Parse("40000000-0000-0000-0000-000000000014");
    private static readonly Guid ScheduledTaskMenuId = Guid.Parse("40000000-0000-0000-0000-000000000006");
    private static readonly Guid OperationLogMenuId = Guid.Parse("40000000-0000-0000-0000-000000000007");
    private static readonly Guid LoginLogMenuId = Guid.Parse("40000000-0000-0000-0000-000000000008");
    private static readonly Guid DemoScheduledTaskId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IDistributedLock _distributedLock;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeedDataInitializer> _logger;

    public SeedDataInitializer(
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IOpenIddictApplicationManager applicationManager,
        IDistributedLock distributedLock,
        IConfiguration configuration,
        ILogger<SeedDataInitializer> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _applicationManager = applicationManager;
        _distributedLock = distributedLock;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _distributedLock.ExecuteWithLockAsync(
            "seed-data:initialize",
            async token =>
            {
                await SeedTenantAsync(token);
                await SeedDepartmentAsync(token);
                await SeedRoleAsync(token);
                await SeedAdminUserAsync(token);
                await SeedPermissionsAsync(token);
                await SeedDictionariesAsync(token);
                await SeedNotificationTemplatesAsync(token);
                await SeedMenusAsync(token);
                await SeedScheduledTasksAsync(token);
                await SeedRoleRelationsAsync(token);
                await SeedOAuthClientAsync(token);

                _logger.LogInformation("Development seed data initialization completed.");
            },
            TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(30),
            cancellationToken);
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

    private async Task SeedDepartmentAsync(CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.Code == "root",
            cancellationToken);
        if (department is not null)
        {
            department.Name = "根部门";
            department.ParentId = null;
            department.Sort = 1;
            department.TreePath = $"/{department.Id}/";
            department.Status = "Enabled";
            department.IsEnabled = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _dbContext.Departments.Add(new Department
        {
            Id = DefaultDepartmentId,
            TenantId = DefaultTenantId,
            ParentId = null,
            Code = "root",
            Name = "根部门",
            Sort = 1,
            TreePath = $"/{DefaultDepartmentId}/",
            Status = "Enabled",
            IsEnabled = true
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
                DepartmentId = DefaultDepartmentId,
                UserName = "admin",
                NormalizedUserName = "ADMIN",
                DisplayName = "系统管理员",
                IsEnabled = true
            };

            var adminPassword = _configuration["SeedData:AdminPassword"];
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new InvalidOperationException("SeedData:AdminPassword must be configured before development seed data can be initialized.");
            }

            admin.PasswordHash = _passwordHasher.HashPassword(admin, adminPassword);
            _dbContext.Users.Add(admin);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (admin.DisplayName == "System Administrator")
        {
            admin.DisplayName = "系统管理员";
            admin.DepartmentId = DefaultDepartmentId;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (admin.DepartmentId is null)
        {
            admin.DepartmentId = DefaultDepartmentId;
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
            ("system:user:import", "导入用户", "system:user", "import"),
            ("system:user:export", "导出用户", "system:user", "export"),
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
            ("system:scheduled-task:trigger", "触发定时任务", "system:scheduled-task", "trigger"),
            ("system:operation-log:view", "查看操作日志", "system:operation-log", "view"),
            ("system:login-log:view", "查看登录日志", "system:login-log", "view"),
            ("system:tenant:view", "查看租户", "system:tenant", "view"),
            ("system:tenant:create", "新增租户", "system:tenant", "create"),
            ("system:tenant:update", "编辑租户", "system:tenant", "update"),
            ("system:tenant:disable", "启用或禁用租户", "system:tenant", "disable"),
            ("system:department:view", "查看部门", "system:department", "view"),
            ("system:department:create", "新增部门", "system:department", "create"),
            ("system:department:update", "编辑部门", "system:department", "update"),
            ("system:department:delete", "删除部门", "system:department", "delete"),
            ("system:role:data-scope", "配置角色数据范围", "system:role", "data-scope"),
            ("system:dict:view", "查看字典", "system:dict", "view"),
            ("system:dict:create", "新增字典", "system:dict", "create"),
            ("system:dict:update", "编辑字典", "system:dict", "update"),
            ("system:dict:delete", "删除字典", "system:dict", "delete"),
            ("system:config:view", "查看系统配置", "system:config", "view"),
            ("system:config:create", "新增系统配置", "system:config", "create"),
            ("system:config:update", "编辑系统配置", "system:config", "update"),
            ("system:config:delete", "删除系统配置", "system:config", "delete"),
            ("system:file:view", "查看文件", "system:file", "view"),
            ("system:file:upload", "上传文件", "system:file", "upload"),
            ("system:file:download", "下载文件", "system:file", "download"),
            ("system:file:delete", "删除文件", "system:file", "delete"),
            ("system:outbox:view", "查看发件箱消息", "system:outbox", "view"),
            ("system:inbox:view", "查看收件箱消息", "system:inbox", "view"),
            ("system:health:view", "查看系统健康", "system:health", "view"),
            ("system:job:view", "查看任务", "system:job", "view"),
            ("system:job:trigger", "触发任务", "system:job", "trigger"),
            ("system:notification:view", "查看通知", "system:notification", "view"),
            ("system:notification:send", "发送系统通知", "system:notification", "send"),
            ("system:notification-template:view", "查看通知模板", "system:notification-template", "view"),
            ("system:notification-template:update", "编辑通知模板", "system:notification-template", "update"),
            ("system:online-user:view", "查看在线用户", "system:online-user", "view"),
            ("system:online-user:kickout", "强制在线用户下线", "system:online-user", "kickout")
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
            TenantMenuId,
            SystemManagementMenuId,
            "租户管理",
            "/system/tenants",
            "system/tenant/index",
            null,
            "OfficeBuilding",
            5,
            "Menu",
            "system:tenant:view",
            cancellationToken);

        await EnsureMenuAsync(
            DepartmentMenuId,
            SystemManagementMenuId,
            "部门管理",
            "/system/departments",
            "system/department/index",
            null,
            "OfficeBuilding",
            8,
            "Menu",
            "system:department:view",
            cancellationToken);

        await EnsureMenuAsync(
            DictionaryMenuId,
            SystemManagementMenuId,
            "字典管理",
            "/system/dicts",
            "system/dict/index",
            null,
            "Collection",
            9,
            "Menu",
            "system:dict:view",
            cancellationToken);

        await EnsureMenuAsync(
            SystemConfigMenuId,
            SystemManagementMenuId,
            "系统配置",
            "/system/configs",
            "system/config/index",
            null,
            "Tools",
            11,
            "Menu",
            "system:config:view",
            cancellationToken);

        await EnsureMenuAsync(
            FileMenuId,
            SystemManagementMenuId,
            "文件管理",
            "/system/files",
            "system/file/index",
            null,
            "Folder",
            12,
            "Menu",
            "system:file:view",
            cancellationToken);

        await EnsureMenuAsync(
            OutboxMessageMenuId,
            SystemManagementMenuId,
            "发件箱消息",
            "/system/outbox-messages",
            "system/outbox-message/index",
            null,
            "Message",
            13,
            "Menu",
            "system:outbox:view",
            cancellationToken);

        await EnsureMenuAsync(
            InboxMessageMenuId,
            SystemManagementMenuId,
            "收件箱消息",
            "/system/inbox-messages",
            "system/inbox-message/index",
            null,
            "MessageBox",
            14,
            "Menu",
            "system:inbox:view",
            cancellationToken);

        await EnsureMenuAsync(
            HealthMenuId,
            SystemManagementMenuId,
            "系统健康",
            "/system/health",
            "system/health/index",
            null,
            "Monitor",
            15,
            "Menu",
            "system:health:view",
            cancellationToken);

        await EnsureMenuAsync(
            JobMenuId,
            SystemManagementMenuId,
            "任务管理",
            "/system/jobs",
            "system/job/index",
            null,
            "Timer",
            16,
            "Menu",
            "system:job:view",
            cancellationToken);

        await EnsureMenuAsync(
            NotificationMenuId,
            SystemManagementMenuId,
            "我的通知",
            "/system/notifications",
            "system/notification/index",
            null,
            "Bell",
            17,
            "Menu",
            "system:notification:view",
            cancellationToken);

        await EnsureMenuAsync(
            NotificationAdminMenuId,
            SystemManagementMenuId,
            "通知管理",
            "/system/notification-admin",
            "system/notification-admin/index",
            null,
            "Message",
            18,
            "Menu",
            "system:notification:send",
            cancellationToken);

        await EnsureMenuAsync(
            OnlineUserMenuId,
            SystemManagementMenuId,
            "在线用户",
            "/system/online-users",
            "system/online-user/index",
            null,
            "User",
            19,
            "Menu",
            "system:online-user:view",
            cancellationToken);

        await EnsureMenuAsync(
            Guid.Parse("40000000-0000-0000-0000-000000000002"),
            SystemManagementMenuId,
            "用户管理",
            "/system/users",
            "system/user/index",
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
            "system/role/index",
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
            "system/menu/index",
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
            "system/permission/index",
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

        await EnsureMenuAsync(
            OperationLogMenuId,
            SystemManagementMenuId,
            "操作日志",
            "/system/operation-logs",
            "system/operation-log/index",
            null,
            "Document",
            60,
            "Menu",
            "system:operation-log:view",
            cancellationToken);

        await EnsureMenuAsync(
            LoginLogMenuId,
            SystemManagementMenuId,
            "登录日志",
            "/system/login-logs",
            "system/login-log/index",
            null,
            "DocumentChecked",
            70,
            "Menu",
            "system:login-log:view",
            cancellationToken);
    }

    private async Task SeedScheduledTasksAsync(CancellationToken cancellationToken)
    {
        var existingTask = await _dbContext.ScheduledTasks.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.Code == "demo-minute-log",
            cancellationToken);

        if (existingTask is not null)
        {
            existingTask.Name = "每分钟演示日志任务";
            existingTask.JobType = "DemoLog";
            existingTask.CronExpression = "* * * * *";
            existingTask.Queue = "default";
            existingTask.Description = "用于测试前端配置 Hangfire 周期执行的演示任务。";
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
            Name = "每分钟演示日志任务",
            JobType = "DemoLog",
            CronExpression = "* * * * *",
            Queue = "default",
            Description = "用于测试前端配置 Hangfire 周期执行的演示任务。",
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

    private async Task SeedDictionariesAsync(CancellationToken cancellationToken)
    {
        await EnsureDictionaryTypeAsync(
            "status",
            "状态",
            "通用启用/禁用状态。",
            "Enabled",
            1,
            cancellationToken);

        await EnsureDictionaryItemAsync(
            "status",
            "Enabled",
            "Enabled",
            "#67C23A",
            "success",
            true,
            "Enabled",
            1,
            "启用状态",
            cancellationToken);

        await EnsureDictionaryItemAsync(
            "status",
            "Disabled",
            "Disabled",
            "#909399",
            "info",
            false,
            "Enabled",
            2,
            "禁用状态",
            cancellationToken);

        await EnsureDictionaryTypeAsync(
            "gender",
            "性别",
            "通用性别显示值。",
            "Enabled",
            2,
            cancellationToken);

        await EnsureDictionaryItemAsync(
            "gender",
            "Male",
            "Male",
            "#409EFF",
            "primary",
            false,
            "Enabled",
            1,
            null,
            cancellationToken);

        await EnsureDictionaryItemAsync(
            "gender",
            "Female",
            "Female",
            "#F56C6C",
            "danger",
            false,
            "Enabled",
            2,
            null,
            cancellationToken);

        await EnsureDictionaryItemAsync(
            "gender",
            "Unknown",
            "Unknown",
            "#909399",
            "info",
            true,
            "Enabled",
            3,
            null,
            cancellationToken);
    }

    private async Task EnsureDictionaryTypeAsync(
        string code,
        string name,
        string? description,
        string status,
        int sort,
        CancellationToken cancellationToken)
    {
        var existingType = await _dbContext.DictionaryTypes.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.Code == code,
            cancellationToken);

        if (existingType is not null)
        {
            existingType.Name = name;
            existingType.Description = description;
            existingType.Status = status;
            existingType.Sort = sort;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _dbContext.DictionaryTypes.Add(new DictionaryType
        {
            TenantId = DefaultTenantId,
            Code = code,
            Name = name,
            Description = description,
            Status = status,
            Sort = sort
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDictionaryItemAsync(
        string typeCode,
        string label,
        string value,
        string? color,
        string? cssClass,
        bool isDefault,
        string status,
        int sort,
        string? remark,
        CancellationToken cancellationToken)
    {
        var existingItem = await _dbContext.DictionaryItems.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.TypeCode == typeCode && entity.Value == value,
            cancellationToken);

        if (existingItem is not null)
        {
            existingItem.Label = label;
            existingItem.Color = color;
            existingItem.CssClass = cssClass;
            existingItem.IsDefault = isDefault;
            existingItem.Status = status;
            existingItem.Sort = sort;
            existingItem.Remark = remark;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _dbContext.DictionaryItems.Add(new DictionaryItem
        {
            TenantId = DefaultTenantId,
            TypeCode = typeCode,
            Label = label,
            Value = value,
            Color = color,
            CssClass = cssClass,
            IsDefault = isDefault,
            Status = status,
            Sort = sort,
            Remark = remark
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedNotificationTemplatesAsync(CancellationToken cancellationToken)
    {
        await EnsureNotificationTemplateAsync(
            "system.notice",
            "系统通知",
            "System",
            "系统通知",
            "你有一条新的系统通知。",
            "Enabled",
            1,
            "默认系统通知模板。",
            cancellationToken);

        await EnsureNotificationTemplateAsync(
            "security.alert",
            "安全告警",
            "Security",
            "安全告警",
            "有一条安全事件需要你处理。",
            "Enabled",
            2,
            "默认安全告警模板。",
            cancellationToken);

        await EnsureNotificationTemplateAsync(
            "task.reminder",
            "任务提醒",
            "Task",
            "任务提醒",
            "有一条任务通知等待处理。",
            "Enabled",
            3,
            "默认任务提醒模板。",
            cancellationToken);
    }

    private async Task EnsureNotificationTemplateAsync(
        string code,
        string name,
        string type,
        string titleTemplate,
        string contentTemplate,
        string status,
        int sort,
        string? remark,
        CancellationToken cancellationToken)
    {
        var existingTemplate = await _dbContext.NotificationTemplates.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.Code == code,
            cancellationToken);

        if (existingTemplate is not null)
        {
            existingTemplate.Name = name;
            existingTemplate.Type = type;
            existingTemplate.TitleTemplate = titleTemplate;
            existingTemplate.ContentTemplate = contentTemplate;
            existingTemplate.Status = status;
            existingTemplate.Sort = sort;
            existingTemplate.Remark = remark;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _dbContext.NotificationTemplates.Add(new NotificationTemplate
        {
            TenantId = DefaultTenantId,
            Code = code,
            Name = name,
            Type = type,
            TitleTemplate = titleTemplate,
            ContentTemplate = contentTemplate,
            Status = status,
            Sort = sort,
            Remark = remark
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
        var clientSecret = _configuration["SeedData:OAuthClientSecret"];
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("SeedData:OAuthClientSecret must be configured before development seed data can be initialized.");
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Confidential,
            ClientSecret = clientSecret,
            DisplayName = "权限管理后台",
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
