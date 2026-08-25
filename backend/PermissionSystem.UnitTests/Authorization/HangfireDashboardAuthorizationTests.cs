using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.UnitTests.Authorization;

public sealed class HangfireDashboardAuthorizationTests
{
    [Fact]
    public void AnonymousUser_IsRejected()
    {
        var context = CreateContext("GET");

        Assert.False(HangfireDashboardAuthorizationFilter.Authorize(context));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public void ViewPermission_AllowsReadOnlyDashboardRequest(string method)
    {
        var context = CreateContext(
            method,
            HangfireDashboardAuthorizationFilter.ViewPermission);

        Assert.True(HangfireDashboardAuthorizationFilter.Authorize(context));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public void ViewPermission_RejectsMutatingDashboardRequest(string method)
    {
        var context = CreateContext(
            method,
            HangfireDashboardAuthorizationFilter.ViewPermission);

        Assert.False(HangfireDashboardAuthorizationFilter.Authorize(context));
    }

    [Fact]
    public void ViewAndTriggerPermissions_AllowMutatingDashboardRequest()
    {
        var context = CreateContext(
            "POST",
            HangfireDashboardAuthorizationFilter.ViewPermission,
            HangfireDashboardAuthorizationFilter.TriggerPermission);

        Assert.True(HangfireDashboardAuthorizationFilter.Authorize(context));
    }

    [Fact]
    public void TriggerPermissionWithoutViewPermission_IsRejected()
    {
        var context = CreateContext(
            "POST",
            HangfireDashboardAuthorizationFilter.TriggerPermission);

        Assert.False(HangfireDashboardAuthorizationFilter.Authorize(context));
    }

    [Fact]
    public void SuperAdmin_BypassesDashboardPermissionSplit()
    {
        var context = CreateContext(
            "POST",
            [],
            ClaimConstants.SuperAdminRoleCode);

        Assert.True(HangfireDashboardAuthorizationFilter.Authorize(context));
    }

    private static DefaultHttpContext CreateContext(
        string method,
        params string[] permissions)
    {
        return CreateContext(method, permissions, role: null);
    }

    private static DefaultHttpContext CreateContext(
        string method,
        string[] permissions,
        string? role)
    {
        var claims = permissions
            .Select(permission => new Claim(ClaimConstants.PermissionCode, permission))
            .ToList();
        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(OpenIddictConstants.Claims.Role, role));
        }

        return new DefaultHttpContext
        {
            Request =
            {
                Method = method
            },
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
    }
}
