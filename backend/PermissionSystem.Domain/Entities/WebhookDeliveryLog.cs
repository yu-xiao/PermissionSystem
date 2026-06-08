using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class WebhookDeliveryLog : BaseEntity
{
    public Guid SubscriptionId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int? ResponseStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public int RetryCount { get; set; }
}
