using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Application.Tenants;

namespace PermissionSystem.UnitTests.Authentication;

public sealed class RefreshTokenMiddlewareTests
{
    [Fact]
    public async Task TenantStatusMiddleware_ShouldDeferRefreshGrantToTokenEndpoint()
    {
        var nextInvoked = false;
        var middleware = new TenantStatusMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid(), "UntrustedRequestTenant");

        await middleware.InvokeAsync(CreateRefreshContext(), tenantContext, null!);

        Assert.True(nextInvoked);
    }

    [Fact]
    public async Task IpAccessMiddleware_ShouldDeferRefreshGrantToTokenEndpoint()
    {
        var nextInvoked = false;
        var middleware = new IpAccessMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(CreateRefreshContext(), null!, null!);

        Assert.True(nextInvoked);
    }

    [Fact]
    public async Task UserSessionMiddleware_ShouldIgnoreBearerSessionForRefreshGrant()
    {
        var nextInvoked = false;
        var middleware = new UserSessionMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(CreateRefreshContext(), null!);

        Assert.True(nextInvoked);
    }

    private static DefaultHttpContext CreateRefreshContext()
    {
        var context = new DefaultHttpContext();
        context.Features.Set(new OpenIddictServerAspNetCoreFeature
        {
            Transaction = new OpenIddictServerTransaction
            {
                Request = new OpenIddictRequest
                {
                    GrantType = OpenIddictConstants.GrantTypes.RefreshToken
                }
            }
        });
        return context;
    }
}
