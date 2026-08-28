using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Tenants;

namespace PermissionSystem.Application.AiCenter;

public static class AiCenterDependencyInjection
{
    public static IServiceCollection AddAiCenterCore(this IServiceCollection services)
    {
        services.TryAddScoped<TenantContext>();
        services.TryAddScoped<ITenantContext>(serviceProvider =>
            serviceProvider.GetRequiredService<TenantContext>());
        services.TryAddScoped<ITraceContextAccessor, TraceContextAccessor>();
        services.TryAddScoped<IAuditContext, NullAuditContext>();
        services.TryAddScoped<IAiToolService, AiToolService>();
        return services;
    }
}
