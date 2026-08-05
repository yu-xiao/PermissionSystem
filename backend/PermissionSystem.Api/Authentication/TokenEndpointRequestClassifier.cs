using Microsoft.AspNetCore;
using OpenIddict.Abstractions;

namespace PermissionSystem.Api.Authentication;

internal static class TokenEndpointRequestClassifier
{
    public static bool IsRefreshTokenGrant(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetOpenIddictServerRequest()?.IsRefreshTokenGrantType() == true;
    }
}
