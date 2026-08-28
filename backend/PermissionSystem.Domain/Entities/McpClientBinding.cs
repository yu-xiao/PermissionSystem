using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class McpClientBinding : BaseEntity
{
    public Guid ApiClientId { get; set; }

    public string OAuthClientId { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
