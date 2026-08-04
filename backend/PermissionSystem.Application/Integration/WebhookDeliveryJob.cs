using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;

namespace PermissionSystem.Application.Integration;

public sealed class WebhookDeliveryJob
{
    private readonly IOpenIntegrationService _openIntegrationService;
    private readonly ISystemTenantScope _systemTenantScope;

    public WebhookDeliveryJob(
        IOpenIntegrationService openIntegrationService,
        ISystemTenantScope systemTenantScope)
    {
        _openIntegrationService = openIntegrationService;
        _systemTenantScope = systemTenantScope;
    }

    public async Task DeliverAsync(Guid subscriptionId, string eventType, string payload, int attempt)
    {
        using var systemScope = _systemTenantScope.Begin(SystemTenantOperations.WebhookDelivery);
        await _openIntegrationService.DeliverWebhookAsync(subscriptionId, eventType, payload, attempt);
    }
}
