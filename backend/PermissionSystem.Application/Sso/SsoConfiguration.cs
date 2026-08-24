namespace PermissionSystem.Application.Sso;

public interface ISsoConfiguration
{
    bool Enabled { get; }

    bool EnableOidc { get; }

    bool EnableSaml { get; }

    string DefaultCallbackPath { get; }

    bool RequireHttpsMetadata { get; }

    bool EncryptClientSecret { get; }

    bool AllowAutoCreateUser { get; }

    bool AllowLocalLoginFallback { get; }
}

public sealed class DefaultSsoConfiguration : ISsoConfiguration
{
    public bool Enabled => true;

    public bool EnableOidc => true;

    public bool EnableSaml => false;

    public string DefaultCallbackPath => "/api/sso/oidc/callback";

    public bool RequireHttpsMetadata => false;

    public bool EncryptClientSecret => true;

    public bool AllowAutoCreateUser => true;

    public bool AllowLocalLoginFallback => true;
}
