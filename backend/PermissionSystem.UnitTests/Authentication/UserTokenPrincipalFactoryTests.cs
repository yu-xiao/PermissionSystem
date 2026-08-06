using System.Security.Claims;
using OpenIddict.Abstractions;
using PermissionSystem.Api.Authentication;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.UnitTests.Authentication;

public sealed class UserTokenPrincipalFactoryTests
{
    [Fact]
    public void RefreshUserState_ShouldReplaceDynamicClaimsAndPreserveTokenMetadata()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var securityStamp = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            "test",
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Subject, userId.ToString());
        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Name, "old-user");
        AddAccessTokenClaim(identity, ClaimConstants.UserId, userId.ToString());
        AddAccessTokenClaim(identity, ClaimConstants.Username, "old-user");
        AddAccessTokenClaim(identity, ClaimConstants.TenantId, tenantId.ToString());
        AddAccessTokenClaim(identity, ClaimConstants.SecurityStamp, Guid.NewGuid().ToString("N"));
        AddAccessTokenClaim(identity, ClaimConstants.DepartmentId, Guid.NewGuid().ToString());
        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Role, "old-role");
        AddAccessTokenClaim(identity, ClaimConstants.PermissionCode, "old:permission");
        AddAccessTokenClaim(identity, ClaimConstants.SessionId, "session-1");
        AddAccessTokenClaim(identity, ClaimConstants.AccessTokenId, "access-1");
        AddAccessTokenClaim(identity, ClaimConstants.RefreshTokenId, "refresh-1");
        identity.AddClaim(new Claim("openiddict-private-metadata", "preserved"));

        var original = new ClaimsPrincipal(identity);
        original.SetScopes(OpenIddictConstants.Scopes.OfflineAccess, "permission-system-api");
        original.SetResources("permission-system-api");
        var user = new AuthenticatedUser(
            userId,
            "current-user",
            tenantId,
            null,
            securityStamp,
            ["current-role"],
            ["current:permission"]);

        var refreshed = UserTokenPrincipalFactory.RefreshUserState(original, user);

        Assert.NotSame(original, refreshed);
        Assert.Equal("old-user", original.FindFirst(OpenIddictConstants.Claims.Name)?.Value);
        Assert.Equal("current-user", refreshed.FindFirst(OpenIddictConstants.Claims.Name)?.Value);
        Assert.Equal("current-role", refreshed.FindFirst(OpenIddictConstants.Claims.Role)?.Value);
        Assert.Equal("current:permission", refreshed.FindFirst(ClaimConstants.PermissionCode)?.Value);
        Assert.Equal(securityStamp.ToString("N"), refreshed.FindFirst(ClaimConstants.SecurityStamp)?.Value);
        Assert.DoesNotContain(refreshed.FindAll(OpenIddictConstants.Claims.Role), claim => claim.Value == "old-role");
        Assert.DoesNotContain(refreshed.FindAll(ClaimConstants.PermissionCode), claim => claim.Value == "old:permission");
        Assert.Null(refreshed.FindFirst(ClaimConstants.DepartmentId));
        Assert.Equal("session-1", refreshed.FindFirst(ClaimConstants.SessionId)?.Value);
        Assert.Equal("access-1", refreshed.FindFirst(ClaimConstants.AccessTokenId)?.Value);
        Assert.Equal("refresh-1", refreshed.FindFirst(ClaimConstants.RefreshTokenId)?.Value);
        Assert.Equal("preserved", refreshed.FindFirst("openiddict-private-metadata")?.Value);
        Assert.Equal(original.GetScopes().ToArray(), refreshed.GetScopes().ToArray());
        Assert.Equal(original.GetResources().ToArray(), refreshed.GetResources().ToArray());
        Assert.All(
            refreshed.FindAll(ClaimConstants.PermissionCode),
            claim => Assert.Contains(OpenIddictConstants.Destinations.AccessToken, claim.GetDestinations()));
    }

    private static void AddAccessTokenClaim(ClaimsIdentity identity, string type, string value)
    {
        identity.AddClaim(new Claim(type, value).SetDestinations(OpenIddictConstants.Destinations.AccessToken));
    }
}
