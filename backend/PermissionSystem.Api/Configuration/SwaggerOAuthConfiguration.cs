using OpenIddict.Abstractions;
using PermissionSystem.Infrastructure.SeedData;
using PermissionSystem.Shared.Constants;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace PermissionSystem.Api.Configuration;

internal static class SwaggerOAuthConfiguration
{
    public static void Configure(SwaggerUIOptions options, IConfiguration configuration)
    {
        var tenant = configuration["Swagger:TenantCode"]?.Trim();
        if (string.IsNullOrWhiteSpace(tenant))
        {
            throw new InvalidOperationException("Swagger:TenantCode must identify the tenant used for Swagger authorization.");
        }
        options.OAuthClientId(SwaggerOAuthClient.ClientId);
        options.OAuthUsePkce();
        options.OAuthScopes(AiCenterConstants.ApiResource, OpenIddictConstants.Scopes.OfflineAccess);
        options.OAuthAdditionalQueryStringParams(new Dictionary<string, string> { ["tenant"] = tenant });
    }
}
