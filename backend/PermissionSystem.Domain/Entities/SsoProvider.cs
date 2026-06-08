using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class SsoProvider : BaseEntity
{
    public string ProviderCode { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public SsoProviderType ProviderType { get; set; } = SsoProviderType.Oidc;

    public bool Enabled { get; set; } = true;

    public string? Authority { get; set; }

    public string? MetadataAddress { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecretEncrypted { get; set; }

    public string Scopes { get; set; } = "openid profile email";

    public string CallbackPath { get; set; } = "/api/sso/oidc/callback";

    public string ResponseType { get; set; } = "code";

    public bool UsePkce { get; set; } = true;

    public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;

    public string UserIdClaim { get; set; } = "sub";

    public string UserNameClaim { get; set; } = "preferred_username";

    public string EmailClaim { get; set; } = "email";

    public string PhoneClaim { get; set; } = "phone_number";

    public string DisplayNameClaim { get; set; } = "name";

    public string RoleClaim { get; set; } = "roles";

    public string DepartmentClaim { get; set; } = "department";

    public bool AutoCreateUser { get; set; }

    public bool AutoBindUser { get; set; } = true;

    public string? DefaultRoleIds { get; set; }

    public bool AllowLocalLoginFallback { get; set; } = true;

    public string? LogoutRedirectUri { get; set; }

    public string? Remark { get; set; }

    public ICollection<SsoUserBinding> UserBindings { get; set; } = [];

    public ICollection<SsoRoleMapping> RoleMappings { get; set; } = [];

    public ICollection<SsoDepartmentMapping> DepartmentMappings { get; set; } = [];
}
