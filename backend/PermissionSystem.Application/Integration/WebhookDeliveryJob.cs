namespace PermissionSystem.Application.Integration;

public sealed class WebhookDeliveryJob
{
    private readonly IOpenIntegrationService _openIntegrationService;

    public WebhookDeliveryJob(IOpenIntegrationService openIntegrationService)
    {
        _openIntegrationService = openIntegrationService;
    }

    public Task DeliverAsync(Guid subscriptionId, string eventType, string payload, int attempt)
    {
        return _openIntegrationService.DeliverWebhookAsync(subscriptionId, eventType, payload, attempt);
    }
}
