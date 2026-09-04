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
        services.TryAddScoped<IAiReadOnlyToolRegistry, AiReadOnlyToolRegistry>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAiReadOnlyToolHandler, UserSearchAiToolHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAiReadOnlyToolHandler, DepartmentSearchAiToolHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAiReadOnlyToolHandler, RoleSummaryAiToolHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAiReadOnlyToolHandler, LoginLogSummaryAiToolHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAiReadOnlyToolHandler, OperationLogSummaryAiToolHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAiReadOnlyToolHandler, ReportDatasetQueryAiToolHandler>());
        return services;
    }
}
