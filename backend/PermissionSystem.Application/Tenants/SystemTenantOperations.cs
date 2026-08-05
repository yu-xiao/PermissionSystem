namespace PermissionSystem.Application.Tenants;

public static class SystemTenantOperations
{
    public const string SeedDataInitialization = "SeedDataInitialization";
    public const string OutboxPublishing = "OutboxPublishing";
    public const string ScheduledTaskSynchronization = "ScheduledTaskSynchronization";
    public const string ScheduledTaskExecution = "ScheduledTaskExecution";
    public const string WebhookDelivery = "WebhookDelivery";
    public const string TenantInitialization = "TenantInitialization";
}
