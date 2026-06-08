using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class ApiClient : BaseEntity
{
    public string ClientCode { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? AllowedScopes { get; set; }

    public string? AllowedIpList { get; set; }

    public int RateLimitPerMinute { get; set; } = 60;
}
