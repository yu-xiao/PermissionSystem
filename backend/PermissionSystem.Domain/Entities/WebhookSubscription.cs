using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class WebhookSubscription : BaseEntity
{
    public string EventType { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public int RetryCount { get; set; } = 3;
}
