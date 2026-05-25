using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Departments;
using PermissionSystem.Application.Dictionaries;
using PermissionSystem.Application.Excels;
using PermissionSystem.Application.Files;
using PermissionSystem.Application.Jobs;
using PermissionSystem.Application.LoginLogs;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.Menus;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.OperationLogs;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Roles;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Application.SystemConfigs;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Application.Users;
using PermissionSystem.Application.UserSessions;

namespace PermissionSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        bool registerOutboxPublisherJob = false)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITraceContextAccessor, TraceContextAccessor>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IDictionaryService, DictionaryService>();
        services.AddScoped<IExcelService, ExcelService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IDataScopeService, DataScopeService>();
        services.AddScoped<IDataPermissionFilter, DataPermissionFilter>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMeService, MeService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IOperationLogService, OperationLogService>();
        services.AddScoped<ILoginLogService, LoginLogService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationRealtimeSender, NullNotificationRealtimeSender>();
        services.AddScoped<ICurrentUserAppService, CurrentUserAppService>();
        services.AddScoped<IScheduledTaskService, ScheduledTaskService>();
        services.AddScoped<IJobInfoService, JobInfoService>();
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddScoped<DemoScheduledTaskJob>();
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IInboxService, InboxService>();
        if (registerOutboxPublisherJob)
        {
            services.AddScoped<OutboxPublisherJob>();
        }

        return services;
    }
}
