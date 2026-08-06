using Microsoft.Extensions.Diagnostics.HealthChecks;
using PermissionSystem.Application.Notifications;

namespace PermissionSystem.Infrastructure.HealthChecks;

public sealed class NotificationDeliveryHealthCheck : IHealthCheck
{
    private readonly NotificationDeliveryOptions _options;

    public NotificationDeliveryHealthCheck(NotificationDeliveryOptions options)
    {
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = new Dictionary<string, object>
        {
            ["mode"] = _options.DeliveryMode.ToString(),
            ["enabled"] = _options.DeliveryMode != NotificationDeliveryMode.Disabled
        };

        var result = _options.DeliveryMode == NotificationDeliveryMode.Disabled
            ? HealthCheckResult.Degraded("Notification delivery is disabled.", data: data)
            : HealthCheckResult.Healthy(
                _options.DeliveryMode == NotificationDeliveryMode.Direct
                    ? "Notifications are persisted directly."
                    : "Notifications are delivered through Outbox and RabbitMQ.",
                data);

        return Task.FromResult(result);
    }
}
