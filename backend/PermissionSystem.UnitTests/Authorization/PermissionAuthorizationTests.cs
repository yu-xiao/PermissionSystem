using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using PermissionSystem.Api.Authorization;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Authorization;

public sealed class PermissionAuthorizationTests
{
    [Fact]
    public void PermissionAttribute_ShouldBuildPermissionPolicy()
    {
        var attribute = new PermissionAttribute("system:user:view");

        Assert.Equal("system:user:view", attribute.PermissionCode);
        Assert.Equal("Permission:system:user:view", attribute.Policy);
    }

    [Fact]
    public async Task PermissionAuthorizationHandler_ShouldSucceed_WhenUserHasPermission()
    {
        var requirement = new PermissionRequirement("system:user:view");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(), resource: null);
        var handler = new PermissionAuthorizationHandler(
            new TestCurrentUserService(permissions: ["system:user:view"]));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task PermissionAuthorizationHandler_ShouldForbid_WhenUserMissesPermission()
    {
        var requirement = new PermissionRequirement("system:user:delete");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(), resource: null);
        var handler = new PermissionAuthorizationHandler(
            new TestCurrentUserService(permissions: ["system:user:view"]));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task PermissionAuthorizationHandler_ShouldAllowSuperAdmin()
    {
        var requirement = new PermissionRequirement("system:user:delete");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(), resource: null);
        var handler = new PermissionAuthorizationHandler(
            new TestCurrentUserService(isSuperAdmin: true));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }
}
