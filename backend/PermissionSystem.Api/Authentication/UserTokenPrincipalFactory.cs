using System.Security.Claims;
using OpenIddict.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Authentication;

internal static class UserTokenPrincipalFactory
{
    public static ClaimsPrincipal RefreshUserState(
        ClaimsPrincipal principal,
        AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(user);

        var refreshedPrincipal = principal.Clone(claim => !IsUserStateClaim(claim.Type));
        var identity = refreshedPrincipal.Identity as ClaimsIdentity
            ?? throw new InvalidOperationException("The token principal does not contain a claims identity.");
        AddUserStateClaims(identity, user);
        return refreshedPrincipal;
    }

    internal static void AddUserStateClaims(ClaimsIdentity identity, AuthenticatedUser user)
    {
        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Subject, user.UserId.ToString());
        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Name, user.Username);
        AddAccessTokenClaim(identity, ClaimConstants.UserId, user.UserId.ToString());
        AddAccessTokenClaim(identity, ClaimConstants.Username, user.Username);
        AddAccessTokenClaim(identity, ClaimConstants.TenantId, user.TenantId.ToString());
        if (user.DepartmentId.HasValue)
        {
            AddAccessTokenClaim(identity, ClaimConstants.DepartmentId, user.DepartmentId.Value.ToString());
        }

        foreach (var role in user.Roles)
        {
            AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Role, role);
        }

        foreach (var permissionCode in user.PermissionCodes)
        {
            AddAccessTokenClaim(identity, ClaimConstants.PermissionCode, permissionCode);
        }
    }

    private static void AddAccessTokenClaim(ClaimsIdentity identity, string type, string value)
    {
        identity.AddClaim(new Claim(type, value).SetDestinations(OpenIddictConstants.Destinations.AccessToken));
    }

    private static bool IsUserStateClaim(string type)
    {
        return type is OpenIddictConstants.Claims.Subject or
            OpenIddictConstants.Claims.Name or
            OpenIddictConstants.Claims.Role or
            ClaimConstants.UserId or
            ClaimConstants.Username or
            ClaimConstants.LegacyUsername or
            ClaimConstants.TenantId or
            ClaimConstants.DepartmentId or
            ClaimConstants.PermissionCode;
    }
}
