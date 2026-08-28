using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Shared.Constants;
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
    private static readonly Guid NumberRuleMenuId = Guid.Parse("40000000-0000-0000-0000-00000000001E");
    private static readonly Guid StateMachineMenuId = Guid.Parse("40000000-0000-0000-0000-00000000001F");
    private static readonly Guid PrintTemplateMenuId = Guid.Parse("40000000-0000-0000-0000-000000000020");
    private static readonly Guid ReportCenterMenuId = Guid.Parse("40000000-0000-0000-0000-000000000021");
    private static readonly Guid ReportDefinitionMenuId = Guid.Parse("40000000-0000-0000-0000-000000000022");
    private static readonly Guid ReportViewerMenuId = Guid.Parse("40000000-0000-0000-0000-000000000023");
    private static readonly Guid SecurityCenterMenuId = Guid.Parse("40000000-0000-0000-0000-000000000024");
    private static readonly Guid SecurityPolicyMenuId = Guid.Parse("40000000-0000-0000-0000-000000000025");
    private static readonly Guid IpAccessRuleMenuId = Guid.Parse("40000000-0000-0000-0000-000000000026");
    private static readonly Guid LoginFailureMenuId = Guid.Parse("40000000-0000-0000-0000-000000000027");
    private static readonly Guid SsoCenterMenuId = Guid.Parse("40000000-0000-0000-0000-00000000002C");
    private static readonly Guid SsoProviderMenuId = Guid.Parse("40000000-0000-0000-0000-00000000002D");
    private static readonly Guid SsoUserBindingMenuId = Guid.Parse("40000000-0000-0000-0000-00000000002E");
    private static readonly Guid SsoRoleMappingMenuId = Guid.Parse("40000000-0000-0000-0000-00000000002F");
    private static readonly Guid SsoDepartmentMappingMenuId = Guid.Parse("40000000-0000-0000-0000-000000000030");
    private static readonly Guid SsoLoginLogMenuId = Guid.Parse("40000000-0000-0000-0000-000000000031");
    private static readonly Guid IntegrationCenterMenuId = Guid.Parse("40000000-0000-0000-0000-000000000028");
    private static readonly Guid IntegrationClientMenuId = Guid.Parse("40000000-0000-0000-0000-000000000029");
    private static readonly Guid IntegrationWebhookMenuId = Guid.Parse("40000000-0000-0000-0000-00000000002A");
    private static readonly Guid IntegrationLogMenuId = Guid.Parse("40000000-0000-0000-0000-00000000002B");
    private static readonly Guid FileMenuId = Guid.Parse("40000000-0000-0000-0000-00000000000D");
    private static readonly Guid OutboxMessageMenuId = Guid.Parse("40000000-0000-0000-0000-00000000000E");
    private static readonly Guid InboxMessageMenuId = Guid.Parse("40000000-0000-0000-0000-00000000000F");
    private static readonly Guid DeadLetterMessageMenuId = Guid.Parse("40000000-0000-0000-0000-000000000033");
    private static readonly Guid HealthMenuId = Guid.Parse("40000000-0000-0000-0000-000000000010");
    private static readonly Guid JobMenuId = Guid.Parse("40000000-0000-0000-0000-000000000011");
    private static readonly Guid NotificationMenuId = Guid.Parse("40000000-0000-0000-0000-000000000012");
    private static readonly Guid NotificationAdminMenuId = Guid.Parse("40000000-0000-0000-0000-000000000013");
    private static readonly Guid OnlineUserMenuId = Guid.Parse("40000000-0000-0000-0000-000000000014");
    private static readonly Guid WorkflowManagementMenuId = Guid.Parse("40000000-0000-0000-0000-000000000015");
    private static readonly Guid WorkflowDefinitionMenuId = Guid.Parse("40000000-0000-0000-0000-000000000016");
    private static readonly Guid WorkflowTaskTodoMenuId = Guid.Parse("40000000-0000-0000-0000-000000000017");
    private static readonly Guid WorkflowTaskDoneMenuId = Guid.Parse("40000000-0000-0000-0000-000000000018");
    private static readonly Guid WorkflowMyStartedMenuId = Guid.Parse("40000000-0000-0000-0000-000000000019");
    private static readonly Guid WorkflowCcMenuId = Guid.Parse("40000000-0000-0000-0000-00000000001A");
    private static readonly Guid WorkflowBusinessBindingMenuId = Guid.Parse("40000000-0000-0000-0000-00000000001B");
    private static readonly Guid DemoManagementMenuId = Guid.Parse("40000000-0000-0000-0000-00000000001C");
    private static readonly Guid DemoApprovalOrderMenuId = Guid.Parse("40000000-0000-0000-0000-00000000001D");
    private static readonly Guid DemoBusinessOrderMenuId = Guid.Parse("40000000-0000-0000-0000-000000000032");
    private static readonly Guid ScheduledTaskMenuId = Guid.Parse("40000000-0000-0000-0000-000000000006");
    private static readonly Guid OperationLogMenuId = Guid.Parse("40000000-0000-0000-0000-000000000007");
    private static readonly Guid LoginLogMenuId = Guid.Parse("40000000-0000-0000-0000-000000000008");
    private static readonly Guid AiProviderMenuId = Guid.Parse("40000000-0000-0000-0000-000000000034");
    private static readonly Guid DemoScheduledTaskId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoStateMachineId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoApprovalOrderNumberRuleId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    private static readonly Guid DemoBusinessOrderStateMachineId = Guid.Parse("60000000-0000-0000-0000-000000000003");
    private static readonly Guid DemoBusinessOrderNumberRuleId = Guid.Parse("60000000-0000-0000-0000-000000000004");
    private static readonly Guid DemoBusinessOrderWorkflowDefinitionId = Guid.Parse("60000000-0000-0000-0000-000000000005");
    private static readonly Guid DemoBusinessOrderWorkflowStartNodeId = Guid.Parse("60000000-0000-0000-0000-000000000006");
    private static readonly Guid DemoBusinessOrderWorkflowApproverNodeId = Guid.Parse("60000000-0000-0000-0000-000000000007");
    private static readonly Guid DemoBusinessOrderWorkflowEndNodeId = Guid.Parse("60000000-0000-0000-0000-000000000008");
    private static readonly Guid DemoBusinessOrderWorkflowStartEdgeId = Guid.Parse("60000000-0000-0000-0000-000000000009");
    private static readonly Guid DemoBusinessOrderWorkflowEndEdgeId = Guid.Parse("60000000-0000-0000-0000-00000000000A");
    private static readonly Guid DemoBusinessOrderWorkflowBindingId = Guid.Parse("60000000-0000-0000-0000-00000000000B");
    private static readonly Guid UserListReportId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid LoginLogReportId = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid OperationLogReportId = Guid.Parse("70000000-0000-0000-0000-000000000003");

    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IDistributedLock _distributedLock;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeedDataInitializer> _logger;
    private readonly ISystemTenantScope _systemTenantScope;

    public SeedDataInitializer(
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IOpenIddictApplicationManager applicationManager,
        IDistributedLock distributedLock,
        IConfiguration configuration,
        ILogger<SeedDataInitializer> logger,
        ISystemTenantScope systemTenantScope)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _applicationManager = applicationManager;
        _distributedLock = distributedLock;
        _configuration = configuration;
        _logger = logger;
        _systemTenantScope = systemTenantScope;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var systemScope = _systemTenantScope.Begin(SystemTenantOperations.SeedDataInitialization);
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
                await SeedReportsAsync(token);
                await SeedSecurityPolicyAsync(token);
                await SeedNumberRulesAsync(token);
                await SeedStateMachinesAsync(token);
                await SeedWorkflowDefinitionsAsync(token);
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
            tenant.Status = TenantStatus.Active;
            tenant.InitializationStep = "Completed";
            tenant.InitializationProgress = 100;
            tenant.InitializedAt ??= DateTimeOffset.UtcNow;
            tenant.StatusChangedAt = tenant.StatusChangedAt == default ? DateTimeOffset.UtcNow : tenant.StatusChangedAt;
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
            Status = TenantStatus.Active,
            InitializationStep = "Completed",
            InitializationProgress = 100,
            InitializedAt = DateTimeOffset.UtcNow,
            StatusChangedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRoleAsync(CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.Code == SystemBuiltinConstants.SuperAdminRoleCode,
            cancellationToken);
        if (role is not null)
        {
            role.Name = "超级管理员";
            role.Description = "系统内置超级管理员角色。";
            role.IsEnabled = true;
            role.IsBuiltin = true;
            role.Sort = 1;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _dbContext.Roles.Add(new Role
        {
            Id = SuperAdminRoleId,
            TenantId = DefaultTenantId,
            Code = SystemBuiltinConstants.SuperAdminRoleCode,
            Name = "超级管理员",
            Description = "系统内置超级管理员角色。",
            IsEnabled = true,
            IsBuiltin = true,
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
            .FirstOrDefaultAsync(
                entity => entity.TenantId == DefaultTenantId &&
                    entity.NormalizedUserName == SystemBuiltinConstants.AdminNormalizedUserName,
                cancellationToken);

        if (admin is null)
        {
            admin = new User
            {
                Id = AdminUserId,
                TenantId = DefaultTenantId,
                DepartmentId = DefaultDepartmentId,
                UserName = SystemBuiltinConstants.AdminUserName,
                NormalizedUserName = SystemBuiltinConstants.AdminNormalizedUserName,
                DisplayName = "系统管理员",
                IsEnabled = true,
                IsBuiltin = true
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
            admin.IsEnabled = true;
            admin.IsBuiltin = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (admin.DepartmentId is null)
        {
            admin.DepartmentId = DefaultDepartmentId;
            admin.IsEnabled = true;
            admin.IsBuiltin = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!admin.IsBuiltin || !admin.IsEnabled)
        {
            admin.IsEnabled = true;
            admin.IsBuiltin = true;
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
            ("sso:provider:view", "View SSO provider", "sso:provider", "view"),
            ("sso:provider:create", "Create SSO provider", "sso:provider", "create"),
            ("sso:provider:update", "Update SSO provider", "sso:provider", "update"),
            ("sso:provider:delete", "Delete SSO provider", "sso:provider", "delete"),
            ("sso:provider:enable", "Enable SSO provider", "sso:provider", "enable"),
            ("sso:provider:disable", "Disable SSO provider", "sso:provider", "disable"),
            ("sso:provider:test", "Test SSO provider", "sso:provider", "test"),
            ("sso:user-binding:view", "View SSO user binding", "sso:user-binding", "view"),
            ("sso:user-binding:unbind", "Unbind SSO user binding", "sso:user-binding", "unbind"),
            ("sso:role-mapping:view", "View SSO role mapping", "sso:role-mapping", "view"),
            ("sso:role-mapping:update", "Update SSO role mapping", "sso:role-mapping", "update"),
            ("sso:department-mapping:view", "View SSO department mapping", "sso:department-mapping", "view"),
            ("sso:department-mapping:update", "Update SSO department mapping", "sso:department-mapping", "update"),
            ("sso:login-log:view", "View SSO login log", "sso:login-log", "view"),
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
            ("system:role:permission-matrix", "角色权限矩阵", "system:role", "permission-matrix"),
            ("system:role:assign-permission", "分配角色权限", "system:role", "assign-permission"),
            ("system:role:assign-user", "关联角色用户", "system:role", "assign-user"),
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
            ("system:number-rule:view", "查看编号规则", "system:number-rule", "view"),
            ("system:number-rule:create", "新增编号规则", "system:number-rule", "create"),
            ("system:number-rule:update", "编辑编号规则", "system:number-rule", "update"),
            ("system:number-rule:delete", "删除编号规则", "system:number-rule", "delete"),
            ("system:number-rule:enable", "启用编号规则", "system:number-rule", "enable"),
            ("system:number-rule:disable", "禁用编号规则", "system:number-rule", "disable"),
            ("system:number-rule:preview", "预览编号规则", "system:number-rule", "preview"),
            ("system:number-rule:generate", "生成测试编号", "system:number-rule", "generate"),
            ("system:number-rule:reset", "重置编号流水", "system:number-rule", "reset"),
            ("system:state-machine:view", "查看状态机", "system:state-machine", "view"),
            ("system:state-machine:create", "新增状态机", "system:state-machine", "create"),
            ("system:state-machine:update", "编辑状态机", "system:state-machine", "update"),
            ("system:state-machine:delete", "删除状态机", "system:state-machine", "delete"),
            ("system:state-machine:transition", "执行业务状态流转", "system:state-machine", "transition"),
            ("system:state-machine:log", "查看状态流转日志", "system:state-machine", "log"),
            ("system:print-template:view", "查看打印模板", "system:print-template", "view"),
            ("system:print-template:create", "新增打印模板", "system:print-template", "create"),
            ("system:print-template:update", "编辑打印模板", "system:print-template", "update"),
            ("system:print-template:delete", "删除打印模板", "system:print-template", "delete"),
            ("system:print-template:design", "设计打印模板", "system:print-template", "design"),
            ("system:print-template:preview", "预览打印模板", "system:print-template", "preview"),
            ("system:print-template:print", "打印模板渲染", "system:print-template", "print"),
            ("system:print-record:view", "查看打印记录", "system:print-record", "view"),
            ("report:definition:view", "查看报表定义", "report:definition", "view"),
            ("report:definition:create", "新增报表定义", "report:definition", "create"),
            ("report:definition:update", "编辑报表定义", "report:definition", "update"),
            ("report:definition:delete", "删除报表定义", "report:definition", "delete"),
            ("report:view", "查看报表", "report", "view"),
            ("report:export", "导出报表", "report", "export"),
            ("report:log:view", "查看报表执行日志", "report:log", "view"),
            (AiCenterConstants.ChatUsePermission, "使用 AI 问答", "ai:chat", "use"),
            (AiCenterConstants.ConversationViewPermission, "查看本人 AI 会话", "ai:conversation", "view"),
            (AiCenterConstants.ToolQueryPermission, "调用 AI 只读工具", "ai:tool", "query"),
            (AiCenterConstants.UserQueryPermission, "AI 查询用户摘要", "ai:tool", "user-query"),
            (AiCenterConstants.DepartmentQueryPermission, "AI 查询部门摘要", "ai:tool", "department-query"),
            (AiCenterConstants.RoleQueryPermission, "AI 查询角色摘要", "ai:tool", "role-query"),
            (AiCenterConstants.LoginLogQueryPermission, "AI 查询登录统计", "ai:tool", "login-log-query"),
            (AiCenterConstants.OperationLogQueryPermission, "AI 查询操作统计", "ai:tool", "operation-log-query"),
            (AiCenterConstants.ReportDatasetQueryPermission, "AI 查询批准数据集", "ai:tool", "dataset-query"),
            (AiCenterConstants.ProviderViewPermission, "查看 AI 模型配置", "ai:provider", "view"),
            (AiCenterConstants.ProviderCreatePermission, "新增 AI 模型配置", "ai:provider", "create"),
            (AiCenterConstants.ProviderUpdatePermission, "修改 AI 模型配置", "ai:provider", "update"),
            (AiCenterConstants.ProviderDeletePermission, "删除 AI 模型配置", "ai:provider", "delete"),
            (AiCenterConstants.ProviderTestPermission, "测试 AI 模型连接", "ai:provider", "test"),
            (AiCenterConstants.ProviderCompliancePermission, "确认 AI 模型合规", "ai:provider", "compliance"),
            (AiCenterConstants.McpDatasetQueryPermission, "查询 MCP 数据集", "mcp:dataset", "query"),
            ("security:policy:view", "查看安全策略", "security:policy", "view"),
            ("security:policy:update", "修改安全策略", "security:policy", "update"),
            ("security:ip-rule:view", "查看 IP 访问规则", "security:ip-rule", "view"),
            ("security:ip-rule:create", "新增 IP 访问规则", "security:ip-rule", "create"),
            ("security:ip-rule:update", "编辑 IP 访问规则", "security:ip-rule", "update"),
            ("security:ip-rule:delete", "删除 IP 访问规则", "security:ip-rule", "delete"),
            ("security:login-failure:view", "查看登录失败记录", "security:login-failure", "view"),
            ("security:verification:send", "发送敏感操作验证码", "security:verification", "send"),
            ("security:verification:verify", "校验敏感操作验证码", "security:verification", "verify"),
            ("integration:client:view", "查看 API 客户端", "integration:client", "view"),
            ("integration:client:create", "新增 API 客户端", "integration:client", "create"),
            ("integration:client:update", "编辑 API 客户端", "integration:client", "update"),
            ("integration:client:delete", "删除 API 客户端", "integration:client", "delete"),
            ("integration:client:secret", "生成 API 客户端密钥", "integration:client", "secret"),
            ("integration:webhook:view", "查看 Webhook 订阅", "integration:webhook", "view"),
            ("integration:webhook:create", "新增 Webhook 订阅", "integration:webhook", "create"),
            ("integration:webhook:update", "编辑 Webhook 订阅", "integration:webhook", "update"),
            ("integration:webhook:delete", "删除 Webhook 订阅", "integration:webhook", "delete"),
            ("integration:webhook:test", "测试 Webhook 订阅", "integration:webhook", "test"),
            ("integration:log:view", "查看开放集成日志", "integration:log", "view"),
            ("system:file:view", "查看文件", "system:file", "view"),
            ("system:file:upload", "上传文件", "system:file", "upload"),
            ("system:file:download", "下载文件", "system:file", "download"),
            ("system:file:delete", "删除文件", "system:file", "delete"),
            ("system:outbox:view", "查看发件箱消息", "system:outbox", "view"),
            ("system:inbox:view", "查看收件箱消息", "system:inbox", "view"),
            ("system:dead-letter:view", "查看死信消息", "system:dead-letter", "view"),
            ("system:dead-letter:replay", "重放死信消息", "system:dead-letter", "replay"),
            ("system:dead-letter:discard", "放弃死信消息", "system:dead-letter", "discard"),
            ("system:health:view", "查看系统健康", "system:health", "view"),
            ("system:job:view", "查看任务", "system:job", "view"),
            ("system:job:trigger", "触发任务", "system:job", "trigger"),
            ("system:notification:view", "查看通知", "system:notification", "view"),
            ("system:notification:send", "发送系统通知", "system:notification", "send"),
            ("system:notification-template:view", "查看通知模板", "system:notification-template", "view"),
            ("system:notification-template:update", "编辑通知模板", "system:notification-template", "update"),
            ("system:online-user:view", "查看在线用户", "system:online-user", "view"),
            ("system:online-user:kickout", "强制在线用户下线", "system:online-user", "kickout"),
            ("workflow:definition:view", "查看流程定义", "workflow:definition", "view"),
            ("workflow:definition:create", "新增流程定义", "workflow:definition", "create"),
            ("workflow:definition:update", "编辑流程定义", "workflow:definition", "update"),
            ("workflow:definition:delete", "删除流程定义", "workflow:definition", "delete"),
            ("workflow:definition:publish", "发布流程定义", "workflow:definition", "publish"),
            ("workflow:definition:disable", "停用流程定义", "workflow:definition", "disable"),
            ("workflow:definition:design", "设计流程定义", "workflow:definition", "design"),
            ("workflow:task:todo", "查看审批任务", "workflow:task", "todo"),
            ("workflow:task:approve", "审批通过", "workflow:task", "approve"),
            ("workflow:task:reject", "审批拒绝", "workflow:task", "reject"),
            ("workflow:task:transfer", "转交审批任务", "workflow:task", "transfer"),
            ("workflow:task:add-sign", "加签审批任务", "workflow:task", "add-sign"),
            ("workflow:instance:start", "发起审批流程", "workflow:instance", "start"),
            ("workflow:instance:view", "查看审批流程", "workflow:instance", "view"),
            ("workflow:instance:withdraw", "撤回审批流程", "workflow:instance", "withdraw"),
            ("workflow:cc:view", "查看抄送流程", "workflow:cc", "view"),
            ("workflow:business-binding:view", "查看业务流程绑定", "workflow:business-binding", "view"),
            ("workflow:business-binding:create", "新增业务流程绑定", "workflow:business-binding", "create"),
            ("workflow:business-binding:update", "编辑业务流程绑定", "workflow:business-binding", "update"),
            ("workflow:business-binding:delete", "删除业务流程绑定", "workflow:business-binding", "delete"),
            ("workflow:business-binding:enable", "启用业务流程绑定", "workflow:business-binding", "enable"),
            ("workflow:business-binding:disable", "禁用业务流程绑定", "workflow:business-binding", "disable"),
            ("demo-approval-order:view", "查看 Demo 审批单", "demo-approval-order", "view"),
            ("demo-approval-order:create", "新增 Demo 审批单", "demo-approval-order", "create"),
            ("demo-approval-order:update", "编辑 Demo 审批单", "demo-approval-order", "update"),
            ("demo-approval-order:delete", "删除 Demo 审批单", "demo-approval-order", "delete"),
            ("demo-approval-order:submit", "提交 Demo 审批单", "demo-approval-order", "submit"),
            ("demo-approval-order:withdraw", "撤回 Demo 审批单", "demo-approval-order", "withdraw"),
            ("demo-approval-order:cancel", "取消 Demo 审批单", "demo-approval-order", "cancel"),
            ("demo-business-order:view", "查看 Demo 业务单据", "demo-business-order", "view"),
            ("demo-business-order:create", "新增 Demo 业务单据", "demo-business-order", "create"),
            ("demo-business-order:update", "编辑 Demo 业务单据", "demo-business-order", "update"),
            ("demo-business-order:delete", "删除 Demo 业务单据", "demo-business-order", "delete"),
            ("demo-business-order:submit", "提交 Demo 业务单据", "demo-business-order", "submit"),
            ("demo-business-order:withdraw", "撤回 Demo 业务单据", "demo-business-order", "withdraw"),
            ("demo-business-order:cancel", "取消 Demo 业务单据", "demo-business-order", "cancel"),
            ("demo-business-order:import", "导入 Demo 业务单据", "demo-business-order", "import"),
            ("demo-business-order:export", "导出 Demo 业务单据", "demo-business-order", "export"),
            ("demo-business-order:attachment:view", "查看 Demo 业务单据附件", "demo-business-order", "attachment:view"),
            ("demo-business-order:attachment:upload", "上传 Demo 业务单据附件", "demo-business-order", "attachment:upload"),
            ("demo-business-order:print", "打印 Demo 业务单据", "demo-business-order", "print"),
            ("demo-business-order:log:view", "查看 Demo 业务单据操作日志", "demo-business-order", "log:view"),
            ("demo-business-order:history:view", "查看 Demo 业务单据变更历史", "demo-business-order", "history:view"),
            ("demo-business-order:notify", "发送 Demo 业务单据通知", "demo-business-order", "notify")
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
            AiProviderMenuId,
            SystemManagementMenuId,
            "AI 模型配置",
            "/system/ai-providers",
            "ai/provider/index",
            null,
            "ChatDotRound",
            11,
            "Menu",
            AiCenterConstants.ProviderViewPermission,
            cancellationToken);

        await EnsureMenuAsync(
            NumberRuleMenuId,
            SystemManagementMenuId,
            "编号规则",
            "/system/number-rules",
            "system/number-rule/index",
            null,
            "Tickets",
            12,
            "Menu",
            "system:number-rule:view",
            cancellationToken);

        await EnsureMenuAsync(
            StateMachineMenuId,
            SystemManagementMenuId,
            "状态机",
            "/system/state-machines",
            "system/state-machine/index",
            null,
            "Switch",
            13,
            "Menu",
            "system:state-machine:view",
            cancellationToken);

        await EnsureMenuAsync(
            PrintTemplateMenuId,
            SystemManagementMenuId,
            "打印模板",
            "/system/print-templates",
            "system/print-template/index",
            null,
            "Printer",
            14,
            "Menu",
            "system:print-template:view",
            cancellationToken);

        await EnsureMenuAsync(
            FileMenuId,
            SystemManagementMenuId,
            "文件管理",
            "/system/files",
            "system/file/index",
            null,
            "Folder",
            15,
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
            DeadLetterMessageMenuId,
            SystemManagementMenuId,
            "死信消息",
            "/system/dead-letter-messages",
            "system/dead-letter-message/index",
            null,
            "Warning",
            15,
            "Menu",
            "system:dead-letter:view",
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
            WorkflowManagementMenuId,
            null,
            "审批管理",
            "/workflow",
            "Layout",
            null,
            "Stamp",
            2,
            "Directory",
            null,
            cancellationToken);

        await EnsureMenuAsync(
            WorkflowDefinitionMenuId,
            WorkflowManagementMenuId,
            "流程定义",
            "/workflow/definition",
            "workflow/definition/index",
            null,
            "Share",
            1,
            "Menu",
            "workflow:definition:view",
            cancellationToken);

        await EnsureMenuAsync(
            WorkflowTaskTodoMenuId,
            WorkflowManagementMenuId,
            "待我审批",
            "/workflow/task/todo",
            "workflow/task/todo",
            null,
            "CircleCheck",
            2,
            "Menu",
            "workflow:task:todo",
            cancellationToken);

        await EnsureMenuAsync(
            WorkflowTaskDoneMenuId,
            WorkflowManagementMenuId,
            "我已审批",
            "/workflow/task/done",
            "workflow/task/done",
            null,
            "Finished",
            3,
            "Menu",
            "workflow:task:todo",
            cancellationToken);

        await EnsureMenuAsync(
            WorkflowMyStartedMenuId,
            WorkflowManagementMenuId,
            "我发起的",
            "/workflow/instance/my-started",
            "workflow/instance/my-started",
            null,
            "Promotion",
            4,
            "Menu",
            "workflow:instance:view",
            cancellationToken);

        await EnsureMenuAsync(
            WorkflowCcMenuId,
            WorkflowManagementMenuId,
            "抄送我的",
            "/workflow/cc",
            "workflow/cc/index",
            null,
            "Message",
            5,
            "Menu",
            "workflow:cc:view",
            cancellationToken);

        await EnsureMenuAsync(
            WorkflowBusinessBindingMenuId,
            WorkflowManagementMenuId,
            "业务流程绑定",
            "/workflow/business-binding",
            "workflow/business-binding/index",
            null,
            "Connection",
            6,
            "Menu",
            "workflow:business-binding:view",
            cancellationToken);

        await EnsureMenuAsync(
            ReportCenterMenuId,
            null,
            "报表中心",
            "/report",
            "Layout",
            null,
            "DataAnalysis",
            3,
            "Directory",
            null,
            cancellationToken);

        await EnsureMenuAsync(
            ReportDefinitionMenuId,
            ReportCenterMenuId,
            "报表管理",
            "/report/definition",
            "report/definition/index",
            null,
            "Document",
            1,
            "Menu",
            "report:definition:view",
            cancellationToken);

        await EnsureMenuAsync(
            ReportViewerMenuId,
            ReportCenterMenuId,
            "报表查看",
            "/report/viewer",
            "report/viewer/index",
            null,
            "DataLine",
            2,
            "Menu",
            "report:view",
            cancellationToken);

        await EnsureMenuAsync(
            SecurityCenterMenuId,
            null,
            "安全中心",
            "/security",
            "Layout",
            null,
            "Lock",
            4,
            "Directory",
            null,
            cancellationToken);

        await EnsureMenuAsync(
            SecurityPolicyMenuId,
            SecurityCenterMenuId,
            "安全策略",
            "/security/policy",
            "security/policy/index",
            null,
            "Lock",
            1,
            "Menu",
            "security:policy:view",
            cancellationToken);

        await EnsureMenuAsync(
            IpAccessRuleMenuId,
            SecurityCenterMenuId,
            "IP 黑白名单",
            "/security/ip-rules",
            "security/ip-rule/index",
            null,
            "Connection",
            2,
            "Menu",
            "security:ip-rule:view",
            cancellationToken);

        await EnsureMenuAsync(
            LoginFailureMenuId,
            SecurityCenterMenuId,
            "登录失败记录",
            "/security/login-failures",
            "security/login-failure/index",
            null,
            "Warning",
            3,
            "Menu",
            "security:login-failure:view",
            cancellationToken);

        await EnsureMenuAsync(
            SsoCenterMenuId,
            SecurityCenterMenuId,
            "单点登录",
            "/security/sso",
            "Layout",
            null,
            "Connection",
            4,
            "Directory",
            null,
            cancellationToken);

        await EnsureMenuAsync(
            SsoProviderMenuId,
            SsoCenterMenuId,
            "SSO 提供方",
            "/security/sso/providers",
            "sso/provider/index",
            null,
            "Connection",
            1,
            "Menu",
            "sso:provider:view",
            cancellationToken);

        await EnsureMenuAsync(
            SsoUserBindingMenuId,
            SsoCenterMenuId,
            "用户绑定",
            "/security/sso/user-bindings",
            "sso/user-binding/index",
            null,
            "User",
            2,
            "Menu",
            "sso:user-binding:view",
            cancellationToken);

        await EnsureMenuAsync(
            SsoRoleMappingMenuId,
            SsoCenterMenuId,
            "角色映射",
            "/security/sso/role-mappings",
            "sso/role-mapping/index",
            null,
            "UserFilled",
            3,
            "Menu",
            "sso:role-mapping:view",
            cancellationToken);

        await EnsureMenuAsync(
            SsoDepartmentMappingMenuId,
            SsoCenterMenuId,
            "部门映射",
            "/security/sso/department-mappings",
            "sso/department-mapping/index",
            null,
            "OfficeBuilding",
            4,
            "Menu",
            "sso:department-mapping:view",
            cancellationToken);

        await EnsureMenuAsync(
            SsoLoginLogMenuId,
            SsoCenterMenuId,
            "SSO 登录日志",
            "/security/sso/login-logs",
            "sso/login-log/index",
            null,
            "DocumentChecked",
            5,
            "Menu",
            "sso:login-log:view",
            cancellationToken);

        await EnsureMenuAsync(
            IntegrationCenterMenuId,
            null,
            "开放集成",
            "/integration",
            "Layout",
            null,
            "Link",
            5,
            "Directory",
            null,
            cancellationToken);

        await EnsureMenuAsync(
            IntegrationClientMenuId,
            IntegrationCenterMenuId,
            "API 客户端",
            "/integration/clients",
            "integration/client/index",
            null,
            "Key",
            1,
            "Menu",
            "integration:client:view",
            cancellationToken);

        await EnsureMenuAsync(
            IntegrationWebhookMenuId,
            IntegrationCenterMenuId,
            "Webhook 订阅",
            "/integration/webhooks",
            "integration/webhook/index",
            null,
            "Share",
            2,
            "Menu",
            "integration:webhook:view",
            cancellationToken);

        await EnsureMenuAsync(
            IntegrationLogMenuId,
            IntegrationCenterMenuId,
            "调用日志",
            "/integration/logs",
            "integration/log/index",
            null,
            "Document",
            3,
            "Menu",
            "integration:log:view",
            cancellationToken);

        await EnsureMenuAsync(
            DemoManagementMenuId,
            null,
            "示例模块",
            "/demo",
            "Layout",
            null,
            "Tickets",
            6,
            "Directory",
            null,
            cancellationToken);

        await EnsureMenuAsync(
            DemoApprovalOrderMenuId,
            DemoManagementMenuId,
            "Demo 审批单",
            "/demo/approval-order",
            "demo/approval-order/index",
            null,
            "DocumentChecked",
            1,
            "Menu",
            "demo-approval-order:view",
            cancellationToken);

        await EnsureMenuAsync(
            DemoBusinessOrderMenuId,
            DemoManagementMenuId,
            "Demo 业务单据",
            "/demo/business-order",
            "demo/business-order/index",
            null,
            "Tickets",
            2,
            "Menu",
            "demo-business-order:view",
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

    private async Task SeedReportsAsync(CancellationToken cancellationToken)
    {
        await EnsureReportAsync(
            UserListReportId,
            "SystemUserList",
            "用户列表报表",
            "System",
            "system-users",
            """
            [
              {"key":"UserName","title":"用户名","width":"140"},
              {"key":"DisplayName","title":"显示名称","width":"160"},
              {"key":"Email","title":"邮箱","width":"180"},
              {"key":"PhoneNumber","title":"手机号","width":"140"},
              {"key":"IsEnabled","title":"启用","width":"90"},
              {"key":"CreatedAt","title":"创建时间","width":"180"}
            ]
            """,
            "系统用户列表示例报表。",
            cancellationToken);

        await EnsureReportAsync(
            LoginLogReportId,
            "SystemLoginLogs",
            "登录日志报表",
            "System",
            "system-login-logs",
            """
            [
              {"key":"UserName","title":"用户名","width":"140"},
              {"key":"LoginType","title":"登录类型","width":"120"},
              {"key":"IpAddress","title":"IP","width":"140"},
              {"key":"LoginResult","title":"结果","width":"100"},
              {"key":"FailureReason","title":"失败原因","width":"220"},
              {"key":"CreatedAt","title":"登录时间","width":"180"}
            ]
            """,
            "系统登录日志示例报表。",
            cancellationToken);

        await EnsureReportAsync(
            OperationLogReportId,
            "SystemOperationLogs",
            "操作日志报表",
            "System",
            "system-operation-logs",
            """
            [
              {"key":"UserName","title":"用户","width":"140"},
              {"key":"Module","title":"模块","width":"140"},
              {"key":"Action","title":"操作","width":"140"},
              {"key":"RequestMethod","title":"方法","width":"100"},
              {"key":"StatusCode","title":"状态码","width":"100"},
              {"key":"ElapsedMilliseconds","title":"耗时(ms)","width":"120"},
              {"key":"CreatedAt","title":"时间","width":"180"}
            ]
            """,
            "系统操作日志示例报表。",
            cancellationToken);
    }

    private async Task EnsureReportAsync(
        Guid id,
        string reportCode,
        string reportName,
        string category,
        string datasetKey,
        string columnsJson,
        string remark,
        CancellationToken cancellationToken)
    {
        var report = await _dbContext.ReportDefinitions.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.ReportCode == reportCode,
            cancellationToken);

        if (report is null)
        {
            _dbContext.ReportDefinitions.Add(new ReportDefinition
            {
                Id = id,
                TenantId = DefaultTenantId,
                ReportCode = reportCode,
                ReportName = reportName,
                Category = category,
                DataSourceType = "Sql",
                DatasetKey = datasetKey,
                SqlText = null,
                ColumnsJson = columnsJson.Trim(),
                ParamsJson = "{}",
                IsEnabled = true,
                Remark = remark
            });
        }
        else
        {
            report.ReportName = reportName;
            report.Category = category;
            report.DataSourceType = "Sql";
            report.DatasetKey = datasetKey;
            report.SqlText = null;
            report.ColumnsJson = columnsJson.Trim();
            report.ParamsJson = "{}";
            report.IsEnabled = true;
            report.Remark = remark;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSecurityPolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await _dbContext.SecurityPolicies.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId,
            cancellationToken);
        if (policy is not null)
        {
            return;
        }

        _dbContext.SecurityPolicies.Add(new SecurityPolicy
        {
            TenantId = DefaultTenantId,
            PasswordMinLength = 8,
            RequireDigit = true,
            RequireLowercase = true,
            LoginFailureLockThreshold = 5,
            LoginFailureLockMinutes = 15,
            EnableSensitiveOperationVerify = false,
            EnableIpWhitelist = false,
            EnableIpBlacklist = false
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedNumberRulesAsync(CancellationToken cancellationToken)
    {
        const string ruleCode = "DemoApprovalOrder";
        var rule = await _dbContext.NumberRules.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.RuleCode == ruleCode,
            cancellationToken);

        if (rule is null)
        {
            _dbContext.NumberRules.Add(new NumberRule
            {
                Id = DemoApprovalOrderNumberRuleId,
                TenantId = DefaultTenantId,
                RuleCode = ruleCode,
                RuleName = "Demo 审批单编号",
                BusinessType = "DemoApprovalOrder",
                Prefix = "DAO",
                DateFormat = "yyyyMMdd",
                SequenceLength = 4,
                ResetCycle = NumberRuleResetCycle.Daily,
                Separator = string.Empty,
                IsEnabled = true,
                Remark = "DemoApprovalOrder 端到端示例使用。"
            });
        }
        else
        {
            rule.RuleName = "Demo 审批单编号";
            rule.BusinessType = "DemoApprovalOrder";
            rule.Prefix = "DAO";
            rule.DateFormat = "yyyyMMdd";
            rule.SequenceLength = 4;
            rule.ResetCycle = NumberRuleResetCycle.Daily;
            rule.Separator = string.Empty;
            rule.IsEnabled = true;
            rule.Remark = "DemoApprovalOrder 端到端示例使用。";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        const string demoBusinessRuleCode = "DemoBusinessOrder";
        var demoBusinessRule = await _dbContext.NumberRules.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.RuleCode == demoBusinessRuleCode,
            cancellationToken);

        if (demoBusinessRule is null)
        {
            _dbContext.NumberRules.Add(new NumberRule
            {
                Id = DemoBusinessOrderNumberRuleId,
                TenantId = DefaultTenantId,
                RuleCode = demoBusinessRuleCode,
                RuleName = "Demo 业务单据编号",
                BusinessType = "DemoBusinessOrder",
                Prefix = "DBO",
                DateFormat = "yyyyMMdd",
                SequenceLength = 4,
                ResetCycle = NumberRuleResetCycle.Daily,
                Separator = string.Empty,
                IsEnabled = true,
                Remark = "DemoBusinessOrder business module template."
            });
        }
        else
        {
            demoBusinessRule.RuleName = "Demo 业务单据编号";
            demoBusinessRule.BusinessType = "DemoBusinessOrder";
            demoBusinessRule.Prefix = "DBO";
            demoBusinessRule.DateFormat = "yyyyMMdd";
            demoBusinessRule.SequenceLength = 4;
            demoBusinessRule.ResetCycle = NumberRuleResetCycle.Daily;
            demoBusinessRule.Separator = string.Empty;
            demoBusinessRule.IsEnabled = true;
            demoBusinessRule.Remark = "DemoBusinessOrder business module template.";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedStateMachinesAsync(CancellationToken cancellationToken)
    {
        var machine = await _dbContext.StateMachineDefinitions.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.BusinessType == "DemoApprovalOrder",
            cancellationToken);

        if (machine is null)
        {
            machine = new StateMachineDefinition
            {
                Id = DemoStateMachineId,
                TenantId = DefaultTenantId,
                BusinessType = "DemoApprovalOrder",
                Name = "Demo 审批单状态机",
                Description = "用于验证平台状态机与审批流联动的示例状态机。",
                IsEnabled = true
            };
            _dbContext.StateMachineDefinitions.Add(machine);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            machine.Name = "Demo 审批单状态机";
            machine.Description = "用于验证平台状态机与审批流联动的示例状态机。";
            machine.IsEnabled = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureStateAsync(machine.Id, "Draft", "草稿", "Initial", "#909399", 1, true, false, cancellationToken);
        await EnsureStateAsync(machine.Id, "Pending", "审批中", "Normal", "#E6A23C", 2, false, false, cancellationToken);
        await EnsureStateAsync(machine.Id, "Approved", "已通过", "Final", "#67C23A", 3, false, true, cancellationToken);
        await EnsureStateAsync(machine.Id, "Rejected", "已拒绝", "Normal", "#F56C6C", 4, false, false, cancellationToken);
        await EnsureStateAsync(machine.Id, "Withdrawn", "已撤回", "Normal", "#909399", 5, false, false, cancellationToken);
        await EnsureStateAsync(machine.Id, "Cancelled", "已取消", "Final", "#909399", 6, false, true, cancellationToken);

        await EnsureTransitionAsync(machine.Id, "Draft", "Pending", "Submit", "提交审批", "demo-approval-order:submit", 1, cancellationToken);
        await EnsureTransitionAsync(machine.Id, "Rejected", "Pending", "Submit", "重新提交", "demo-approval-order:submit", 2, cancellationToken);
        await EnsureTransitionAsync(machine.Id, "Withdrawn", "Pending", "Submit", "重新提交", "demo-approval-order:submit", 3, cancellationToken);
        await EnsureTransitionAsync(machine.Id, "Pending", "Approved", "Approve", "审批通过", "workflow:task:approve", 4, cancellationToken);
        await EnsureTransitionAsync(machine.Id, "Pending", "Rejected", "Reject", "审批拒绝", "workflow:task:reject", 5, cancellationToken);
        await EnsureTransitionAsync(machine.Id, "Pending", "Withdrawn", "Withdraw", "撤回审批", "demo-approval-order:withdraw", 6, cancellationToken);
        await EnsureTransitionAsync(machine.Id, "Draft", "Cancelled", "Cancel", "取消", "demo-approval-order:cancel", 7, cancellationToken);

        var demoBusinessMachine = await _dbContext.StateMachineDefinitions.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.BusinessType == "DemoBusinessOrder",
            cancellationToken);

        if (demoBusinessMachine is null)
        {
            demoBusinessMachine = new StateMachineDefinition
            {
                Id = DemoBusinessOrderStateMachineId,
                TenantId = DefaultTenantId,
                BusinessType = "DemoBusinessOrder",
                Name = "Demo 业务单据状态机",
                Description = "Business module template status machine.",
                IsEnabled = true
            };
            _dbContext.StateMachineDefinitions.Add(demoBusinessMachine);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            demoBusinessMachine.Name = "Demo 业务单据状态机";
            demoBusinessMachine.Description = "Business module template status machine.";
            demoBusinessMachine.IsEnabled = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureStateAsync(demoBusinessMachine.Id, "Draft", "草稿", "Initial", "#909399", 1, true, false, cancellationToken);
        await EnsureStateAsync(demoBusinessMachine.Id, "Pending", "审批中", "Normal", "#E6A23C", 2, false, false, cancellationToken);
        await EnsureStateAsync(demoBusinessMachine.Id, "Approved", "已通过", "Final", "#67C23A", 3, false, true, cancellationToken);
        await EnsureStateAsync(demoBusinessMachine.Id, "Rejected", "已拒绝", "Normal", "#F56C6C", 4, false, false, cancellationToken);
        await EnsureStateAsync(demoBusinessMachine.Id, "Withdrawn", "已撤回", "Normal", "#909399", 5, false, false, cancellationToken);
        await EnsureStateAsync(demoBusinessMachine.Id, "Cancelled", "已取消", "Final", "#909399", 6, false, true, cancellationToken);

        await EnsureTransitionAsync(demoBusinessMachine.Id, "Draft", "Pending", "Submit", "提交审批", "demo-business-order:submit", 1, cancellationToken);
        await EnsureTransitionAsync(demoBusinessMachine.Id, "Rejected", "Pending", "Submit", "重新提交", "demo-business-order:submit", 2, cancellationToken);
        await EnsureTransitionAsync(demoBusinessMachine.Id, "Withdrawn", "Pending", "Submit", "重新提交", "demo-business-order:submit", 3, cancellationToken);
        await EnsureTransitionAsync(demoBusinessMachine.Id, "Pending", "Approved", "Approve", "审批通过", "workflow:task:approve", 4, cancellationToken);
        await EnsureTransitionAsync(demoBusinessMachine.Id, "Pending", "Rejected", "Reject", "审批拒绝", "workflow:task:reject", 5, cancellationToken);
        await EnsureTransitionAsync(demoBusinessMachine.Id, "Pending", "Withdrawn", "Withdraw", "撤回审批", "demo-business-order:withdraw", 6, cancellationToken);
        await EnsureTransitionAsync(demoBusinessMachine.Id, "Draft", "Cancelled", "Cancel", "取消", "demo-business-order:cancel", 7, cancellationToken);
    }

    private async Task SeedWorkflowDefinitionsAsync(CancellationToken cancellationToken)
    {
        const string businessType = "DemoBusinessOrder";
        const string definitionCode = "DemoBusinessOrderDefaultApproval";
        const string definitionName = "DemoBusinessOrder 默认审批流";

        var definition = await _dbContext.WorkflowDefinitions.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId &&
                entity.Code == definitionCode &&
                entity.Version == 1,
            cancellationToken);

        if (definition is null)
        {
            definition = new WorkflowDefinition
            {
                Id = DemoBusinessOrderWorkflowDefinitionId,
                TenantId = DefaultTenantId,
                Code = definitionCode,
                Name = definitionName,
                Description = "Default development workflow for DemoBusinessOrder: Start -> SuperAdmin approval -> End.",
                Version = 1,
                Status = WorkflowDefinitionStatus.Published,
                IsPublished = true,
                PublishedAt = DateTimeOffset.UtcNow
            };
            _dbContext.WorkflowDefinitions.Add(definition);
        }
        else
        {
            definition.Name = definitionName;
            definition.Description = "Default development workflow for DemoBusinessOrder: Start -> SuperAdmin approval -> End.";
            definition.Status = WorkflowDefinitionStatus.Published;
            definition.IsPublished = true;
            definition.PublishedAt ??= DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureWorkflowNodeAsync(
            DemoBusinessOrderWorkflowStartNodeId,
            definition.Id,
            "start",
            "Start",
            WorkflowNodeType.Start,
            null,
            null,
            null,
            120,
            160,
            1,
            cancellationToken);

        await EnsureWorkflowNodeAsync(
            DemoBusinessOrderWorkflowApproverNodeId,
            definition.Id,
            "super-admin-approve",
            "SuperAdmin Approval",
            WorkflowNodeType.Approver,
            WorkflowApproverType.Users,
            AdminUserId.ToString(),
            WorkflowApprovalMode.Single,
            360,
            160,
            2,
            cancellationToken);

        await EnsureWorkflowNodeAsync(
            DemoBusinessOrderWorkflowEndNodeId,
            definition.Id,
            "end",
            "End",
            WorkflowNodeType.End,
            null,
            null,
            null,
            600,
            160,
            3,
            cancellationToken);

        await EnsureWorkflowEdgeAsync(
            DemoBusinessOrderWorkflowStartEdgeId,
            definition.Id,
            "start",
            "super-admin-approve",
            1,
            cancellationToken);

        await EnsureWorkflowEdgeAsync(
            DemoBusinessOrderWorkflowEndEdgeId,
            definition.Id,
            "super-admin-approve",
            "end",
            2,
            cancellationToken);

        await EnsureWorkflowBusinessBindingAsync(
            DemoBusinessOrderWorkflowBindingId,
            businessType,
            "DemoBusinessOrder 默认审批",
            definition,
            cancellationToken);
    }

    private async Task EnsureWorkflowNodeAsync(
        Guid id,
        Guid definitionId,
        string nodeKey,
        string nodeName,
        WorkflowNodeType nodeType,
        WorkflowApproverType? approverType,
        string? approverIds,
        WorkflowApprovalMode? approvalMode,
        decimal positionX,
        decimal positionY,
        int sort,
        CancellationToken cancellationToken)
    {
        var node = await _dbContext.WorkflowNodes.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId &&
                entity.DefinitionId == definitionId &&
                entity.NodeKey == nodeKey,
            cancellationToken);

        if (node is null)
        {
            _dbContext.WorkflowNodes.Add(new WorkflowNode
            {
                Id = id,
                TenantId = DefaultTenantId,
                DefinitionId = definitionId,
                NodeKey = nodeKey,
                NodeName = nodeName,
                NodeType = nodeType,
                ApproverType = approverType,
                ApproverIds = approverIds,
                ApprovalMode = approvalMode,
                PositionX = positionX,
                PositionY = positionY,
                Sort = sort
            });
        }
        else
        {
            node.NodeName = nodeName;
            node.NodeType = nodeType;
            node.ApproverType = approverType;
            node.ApproverIds = approverIds;
            node.ApprovalMode = approvalMode;
            node.PositionX = positionX;
            node.PositionY = positionY;
            node.Sort = sort;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureWorkflowEdgeAsync(
        Guid id,
        Guid definitionId,
        string fromNodeKey,
        string toNodeKey,
        int sort,
        CancellationToken cancellationToken)
    {
        var edge = await _dbContext.WorkflowEdges.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId &&
                entity.DefinitionId == definitionId &&
                entity.FromNodeKey == fromNodeKey &&
                entity.ToNodeKey == toNodeKey,
            cancellationToken);

        if (edge is null)
        {
            _dbContext.WorkflowEdges.Add(new WorkflowEdge
            {
                Id = id,
                TenantId = DefaultTenantId,
                DefinitionId = definitionId,
                FromNodeKey = fromNodeKey,
                ToNodeKey = toNodeKey,
                IsDefault = false,
                Sort = sort
            });
        }
        else
        {
            edge.ConditionId = null;
            edge.IsDefault = false;
            edge.Sort = sort;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureWorkflowBusinessBindingAsync(
        Guid id,
        string businessType,
        string businessName,
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        var binding = await _dbContext.WorkflowBusinessBindings.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.BusinessType == businessType,
            cancellationToken);

        if (binding is null)
        {
            _dbContext.WorkflowBusinessBindings.Add(new WorkflowBusinessBinding
            {
                Id = id,
                TenantId = DefaultTenantId,
                BusinessType = businessType,
                BusinessName = businessName,
                DefinitionId = definition.Id,
                DefinitionCode = definition.Code,
                DefinitionName = definition.Name,
                IsEnabled = true,
                Remark = "Seeded default binding for local development DemoBusinessOrder approval."
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var existingDefinition = await _dbContext.WorkflowDefinitions.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.Id == binding.DefinitionId,
            cancellationToken);
        var pointsToPublishedDefinition = existingDefinition is not null &&
            existingDefinition.IsPublished &&
            existingDefinition.Status == WorkflowDefinitionStatus.Published;
        if (binding.IsEnabled && pointsToPublishedDefinition && binding.DefinitionId != definition.Id)
        {
            return;
        }

        binding.BusinessName = businessName;
        binding.DefinitionId = definition.Id;
        binding.DefinitionCode = definition.Code;
        binding.DefinitionName = definition.Name;
        binding.IsEnabled = true;
        binding.Remark = "Seeded default binding for local development DemoBusinessOrder approval.";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureStateAsync(
        Guid machineId,
        string stateCode,
        string stateName,
        string stateType,
        string color,
        int sort,
        bool isInitial,
        bool isFinal,
        CancellationToken cancellationToken)
    {
        var state = await _dbContext.StateDefinitions.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId && entity.MachineId == machineId && entity.StateCode == stateCode,
            cancellationToken);

        if (state is null)
        {
            _dbContext.StateDefinitions.Add(new StateDefinition
            {
                TenantId = DefaultTenantId,
                MachineId = machineId,
                StateCode = stateCode,
                StateName = stateName,
                StateType = stateType,
                Color = color,
                Sort = sort,
                IsInitial = isInitial,
                IsFinal = isFinal
            });
        }
        else
        {
            state.StateName = stateName;
            state.StateType = stateType;
            state.Color = color;
            state.Sort = sort;
            state.IsInitial = isInitial;
            state.IsFinal = isFinal;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTransitionAsync(
        Guid machineId,
        string fromState,
        string toState,
        string actionCode,
        string actionName,
        string requiredPermission,
        int sort,
        CancellationToken cancellationToken)
    {
        var transition = await _dbContext.StateTransitions.FirstOrDefaultAsync(
            entity => entity.TenantId == DefaultTenantId &&
                entity.MachineId == machineId &&
                entity.FromState == fromState &&
                entity.ActionCode == actionCode,
            cancellationToken);

        if (transition is null)
        {
            _dbContext.StateTransitions.Add(new StateTransition
            {
                TenantId = DefaultTenantId,
                MachineId = machineId,
                FromState = fromState,
                ToState = toState,
                ActionCode = actionCode,
                ActionName = actionName,
                RequiredPermission = requiredPermission,
                IsEnabled = true,
                Sort = sort
            });
        }
        else
        {
            transition.ToState = toState;
            transition.ActionName = actionName;
            transition.RequiredPermission = requiredPermission;
            transition.IsEnabled = true;
            transition.Sort = sort;
        }

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
                Permissions.Prefixes.GrantType + PermissionSystem.Application.Sso.SsoGrantTypes.OidcLoginCode,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + Scopes.OpenId,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                Permissions.Prefixes.Scope + AiCenterConstants.ApiResource,
                Permissions.Prefixes.Scope + AiCenterConstants.McpScope
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
        }
        else
        {
            await _applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        }

        await SeedMcpIntrospectionClientAsync(cancellationToken);
    }

    private async Task SeedMcpIntrospectionClientAsync(CancellationToken cancellationToken)
    {
        var clientSecret = _configuration["SeedData:McpIntrospectionClientSecret"];
        if (string.IsNullOrWhiteSpace(clientSecret) || clientSecret.Length < 32)
        {
            throw new InvalidOperationException(
                "SeedData:McpIntrospectionClientSecret must contain at least 32 characters before development seed data can be initialized.");
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = AiCenterConstants.McpIntrospectionClientId,
            ClientType = ClientTypes.Confidential,
            ClientSecret = clientSecret,
            DisplayName = "PermissionSystem MCP Server",
            Permissions =
            {
                Permissions.Endpoints.Introspection
            }
        };

        var application = await _applicationManager.FindByClientIdAsync(
            AiCenterConstants.McpIntrospectionClientId,
            cancellationToken);
        if (application is null)
        {
            await _applicationManager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await _applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        }
    }
}
