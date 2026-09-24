using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using PermissionSystem.Shared.Constants;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PermissionSystem.Infrastructure.SeedData;

public static class SwaggerOAuthClient
{
    public const string ClientId = "permission-swagger";

    internal static OpenIddictApplicationDescriptor? CreateDescriptor(IConfiguration configuration)
    {
        var values = configuration.GetSection("Swagger:OAuthRedirectUris").Get<string[]>() ?? [];
        if (values.Length == 0) return null;

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = "PermissionSystem Swagger",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                Permissions.Prefixes.Scope + AiCenterConstants.ApiResource
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange }
        };
        foreach (var value in values)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
                value.Contains('*') || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                !uri.AbsolutePath.EndsWith("/swagger/oauth2-redirect.html", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Swagger:OAuthRedirectUris must contain explicit Swagger callback URLs using HTTPS or loopback HTTP.");
            }
            descriptor.RedirectUris.Add(uri);
        }
        return descriptor;
    }
}
