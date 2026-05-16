using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application.Menus;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Roles;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Application.Users;

namespace PermissionSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ICurrentUserAppService, CurrentUserAppService>();
        services.AddScoped<IScheduledTaskService, ScheduledTaskService>();
        services.AddScoped<DemoScheduledTaskJob>();

        return services;
    }
}
