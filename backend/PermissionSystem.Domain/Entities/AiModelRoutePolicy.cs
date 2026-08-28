using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class AiModelRoutePolicy : BaseEntity
{
    public string AgentCode { get; set; } = string.Empty;

    public Guid PrimaryProviderConfigId { get; set; }

    public Guid? CanaryProviderConfigId { get; set; }

    public int CanaryPercentage { get; set; }

    public Guid? FallbackProviderConfigId { get; set; }

    public bool IsEnabled { get; set; } = true;
}
