using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Departments;
using PermissionSystem.Application.Dictionaries;
using PermissionSystem.Application.DemoApprovalOrders;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Excels;
using PermissionSystem.Application.Files;
using PermissionSystem.Application.Integration;
using PermissionSystem.Application.Jobs;
using PermissionSystem.Application.LoginLogs;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Application.Menus;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.NumberRules;
using PermissionSystem.Application.OperationLogs;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.PrintTemplates;
using PermissionSystem.Application.Reports;
using PermissionSystem.Application.Roles;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Application.Security;
using PermissionSystem.Application.Sso;
using PermissionSystem.Application.StateMachines;
using PermissionSystem.Application.SystemConfigs;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Application.Users;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        bool registerOutboxPublisherJob = false,
        params Assembly[] moduleAssemblies)
    {
        services.AddAiCenterCore();
        services.AddScoped<IAiProviderService, AiProviderService>();
        services.AddScoped<IAiConversationService, AiConversationService>();
        services.AddScoped<IAiReadOnlyToolRegistry, AiReadOnlyToolRegistry>();
        services.AddScoped<DemoBusinessOrderDraftHandler>();
        services.AddScoped<IAiBusinessActionHandler>(serviceProvider =>
            serviceProvider.GetRequiredService<DemoBusinessOrderDraftHandler>());
        services.AddScoped<IAiDocumentDraftService>(serviceProvider =>
            serviceProvider.GetRequiredService<DemoBusinessOrderDraftHandler>());
        services.AddScoped<IAiDocumentDraftReader>(serviceProvider =>
            serviceProvider.GetRequiredService<DemoBusinessOrderDraftHandler>());
        services.AddScoped<IAiDocumentExecutionService, AiDocumentExecutionService>();
        services.AddScoped<IMcpCallerContext, McpCallerContext>();
        services.AddScoped<IMcpClientAccessService, McpClientAccessService>();
        services.AddScoped<IMcpDatasetProvisioner, McpDatasetProvisioner>();
        services.AddScoped<IMcpAdministrationService, McpAdministrationService>();
        services.AddScoped<IMcpDatasetService, McpDatasetService>();
        services.AddScoped<IAiActionToolRegistry, AiActionToolRegistry>();
        services.AddSingleton<AiRunCancellationCoordinator>();
        services.AddScoped<IAiRunRealtimeSender, NullAiRunRealtimeSender>();
        services.AddScoped<ISystemTenantScope, SystemTenantScope>();
        services.AddScoped<ITenantWriteResolver, TenantWriteResolver>();
        services.TryAddScoped<IAuditContext, NullAuditContext>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<TenantInitializationJob>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IDictionaryService, DictionaryService>();
        services.AddScoped<IExcelService, ExcelService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IFileContentScanner, FileContentScanner>();
        services.AddScoped<IFileBusinessAccessChecker, FileBusinessAccessChecker>();
        services.AddScoped<FileStorageCompensationJob>();
        services.AddScoped<DataScopeService>();
        services.AddScoped<IDataScopeService>(serviceProvider =>
            serviceProvider.GetRequiredService<DataScopeService>());
        services.AddScoped<IUserDataScopeService>(serviceProvider =>
            serviceProvider.GetRequiredService<DataScopeService>());
        services.AddScoped<IDataPermissionFilter, DataPermissionFilter>();
        services.AddScoped(typeof(IDataPermissionRepository<>), typeof(DataPermissionRepository<>));
        services.AddScoped<IDataPermissionSpecification<DemoBusinessOrder>, DemoBusinessOrderDataPermissionSpecification>();
        services.AddScoped<IDataPermissionSpecification<DemoApprovalOrder>, DemoApprovalOrderDataPermissionSpecification>();
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
        services.AddScoped<INumberRuleService, NumberRuleService>();
        services.AddScoped<INumberGenerator, NumberGenerator>();
        services.AddScoped<IStateMachineService, StateMachineService>();
        services.AddScoped<IStateTransitionExecutor, StateTransitionExecutor>();
        services.AddScoped<IStateTransitionHandlerResolver, StateTransitionHandlerResolver>();
        services.AddScoped<IPrintTemplateService, PrintTemplateService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IReadOnlyReportQueryService>(serviceProvider =>
            serviceProvider.GetRequiredService<IReportService>());
        services.AddScoped<ISecurityPolicyService, SecurityPolicyService>();
        services.AddScoped<ISsoProviderService, SsoProviderService>();
        services.AddScoped<ISsoLoginService, SsoLoginService>();
        services.AddScoped<ISsoManagementService, SsoManagementService>();
        services.AddScoped<IApiClientContext, ApiClientContext>();
        services.AddScoped<IOpenIntegrationService, OpenIntegrationService>();
        services.AddScoped<WebhookDeliveryJob>();
        services.AddScoped<DemoScheduledTaskJob>();
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IInboxService, InboxService>();
        services.AddScoped<IDeadLetterMessageService, DeadLetterMessageService>();
        services.AddScoped<IWorkflowDefinitionService, WorkflowDefinitionService>();
        services.AddScoped<IWorkflowBusinessBindingService, WorkflowBusinessBindingService>();
        services.AddScoped<IWorkflowConditionEvaluator, WorkflowConditionEvaluator>();
        services.AddScoped<IWorkflowApproverResolver, WorkflowApproverResolver>();
        services.AddScoped<IWorkflowBusinessHandlerResolver, WorkflowBusinessHandlerResolver>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<IWorkflowTaskService, WorkflowTaskService>();
        services.AddScoped<IDemoApprovalOrderService, DemoApprovalOrderService>();
        services.AddScoped<IDemoBusinessOrderValidator, DemoBusinessOrderValidator>();
        services.AddScoped<IDemoBusinessOrderService, DemoBusinessOrderService>();
        if (registerOutboxPublisherJob)
        {
            services.AddScoped<OutboxPublisherJob>();
        }

        var assemblies = new[] { typeof(DependencyInjection).Assembly }
            .Concat(moduleAssemblies)
            .ToArray();
        services.AddMarkedDependencies(assemblies);

        return services;
    }
}
