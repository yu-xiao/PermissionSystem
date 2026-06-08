namespace PermissionSystem.Infrastructure.Options;

public sealed class SsoOptions
{
    public const string SectionName = "Sso";

    public bool Enabled { get; init; } = true;

    public bool EnableOidc { get; init; } = true;

    public bool EnableSaml { get; init; }

    public string DefaultCallbackPath { get; init; } = "/api/sso/oidc/callback";

    public bool RequireHttpsMetadata { get; init; }

    public bool EncryptClientSecret { get; init; } = true;

    public bool AllowAutoCreateUser { get; init; } = true;

    public bool AllowLocalLoginFallback { get; init; } = true;
}
