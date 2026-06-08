using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class SsoLoginLog : BaseEntity
{
    public string ProviderCode { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public SsoProviderType ProviderType { get; set; } = SsoProviderType.Oidc;

    public string? ExternalUserId { get; set; }

    public string? ExternalUserName { get; set; }

    public Guid? LocalUserId { get; set; }

    public string? LocalUserName { get; set; }

    public SsoLoginResult LoginResult { get; set; } = SsoLoginResult.Failed;

    public string? FailureReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? TraceId { get; set; }
}
