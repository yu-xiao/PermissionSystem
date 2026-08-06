using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Shared.Constants;
using PermissionSystem.UnitTests.TestSupport;

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

        await middleware.InvokeAsync(CreateRefreshContext(), null!, null!);

        Assert.True(nextInvoked);
    }

    [Fact]
    public async Task UserSessionMiddleware_ShouldRejectLegacyAccessTokenWithoutSecurityStamp()
    {
        var nextInvoked = false;
        var checker = new StubUserSessionStatusChecker(UserAccessValidationStatus.Valid);
        var sessions = new TestUserSessionService();
        var middleware = new UserSessionMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        var context = CreateAccessContext(includeSecurityStamp: false);

        await middleware.InvokeAsync(context, checker, sessions);

        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("true", context.Response.Headers["X-Authorization-Stale"]);
        Assert.Equal(0, checker.CallCount);
        Assert.Empty(sessions.TouchedSessionIds);
    }

    [Fact]
    public async Task UserSessionMiddleware_ShouldRejectStaleAuthorizationState()
    {
        var checker = new StubUserSessionStatusChecker(UserAccessValidationStatus.StaleAuthorization);
        var sessions = new TestUserSessionService();
        var middleware = new UserSessionMiddleware(_ => Task.CompletedTask);
        var context = CreateAccessContext();

        await middleware.InvokeAsync(context, checker, sessions);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("true", context.Response.Headers["X-Authorization-Stale"]);
        Assert.Empty(sessions.TouchedSessionIds);
    }

    [Fact]
    public async Task UserSessionMiddleware_ShouldRejectInactiveUserAsRevokedSession()
    {
        var checker = new StubUserSessionStatusChecker(UserAccessValidationStatus.InactiveUser);
        var sessions = new TestUserSessionService();
        var middleware = new UserSessionMiddleware(_ => Task.CompletedTask);
        var context = CreateAccessContext();

        await middleware.InvokeAsync(context, checker, sessions);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("true", context.Response.Headers["X-Session-Revoked"]);
        Assert.Empty(sessions.TouchedSessionIds);
    }

    [Fact]
    public async Task UserSessionMiddleware_ShouldContinueAndTouchValidSession()
    {
        var nextInvoked = false;
        var checker = new StubUserSessionStatusChecker(UserAccessValidationStatus.Valid);
        var sessions = new TestUserSessionService();
        var middleware = new UserSessionMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        var context = CreateAccessContext();

        await middleware.InvokeAsync(context, checker, sessions);

        Assert.True(nextInvoked);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(["session-1"], sessions.TouchedSessionIds);
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

    private static DefaultHttpContext CreateAccessContext(bool includeSecurityStamp = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimConstants.UserId, TestIds.NormalUserId.ToString()),
            new(ClaimConstants.TenantId, TestIds.TenantId.ToString()),
            new(ClaimConstants.SessionId, "session-1")
        };
        if (includeSecurityStamp)
        {
            claims.Add(new Claim(ClaimConstants.SecurityStamp, Guid.NewGuid().ToString("N")));
        }

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
    }

    private sealed class StubUserSessionStatusChecker : IUserSessionStatusChecker
    {
        private readonly UserAccessValidationStatus _status;

        public StubUserSessionStatusChecker(UserAccessValidationStatus status)
        {
            _status = status;
        }

        public int CallCount { get; private set; }

        public Task<UserAccessValidationStatus> ValidateAccessAsync(
            Guid tenantId,
            Guid userId,
            string sessionId,
            Guid securityStamp,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_status);
        }

        public Task<bool> IsValidForRefreshAsync(
            Guid tenantId,
            Guid userId,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
