using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.McpServer.Middlewares;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class McpCallerValidationTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public void TryResolveIdentity_AcceptsMatchingDelegatedIdentity()
    {
        var context = CreateContext();
        context.Request.Headers["X-Tenant-Id"] = TenantId.ToString();

        var succeeded = McpCallerValidationMiddleware.TryResolveIdentity(
            context,
            out var identity,
            out var failure);

        Assert.True(succeeded);
        Assert.Null(failure);
        Assert.Equal(TenantId, identity.TenantId);
    }

    [Fact]
    public void TryResolveIdentity_RejectsTenantHeaderOverride()
    {
        var context = CreateContext();
        context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();

        var succeeded = McpCallerValidationMiddleware.TryResolveIdentity(
            context,
            out _,
            out var failure);

        Assert.False(succeeded);
        Assert.Equal(StatusCodes.Status403Forbidden, failure?.StatusCode);
        Assert.Equal(ErrorCode.Forbidden, failure?.ErrorCode);
    }

    [Fact]
    public void TryResolveIdentity_RejectsServiceActorWithoutUserSession()
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, "service-client"));
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var succeeded = McpCallerValidationMiddleware.TryResolveIdentity(
            context,
            out _,
            out var failure);

        Assert.False(succeeded);
        Assert.Equal(StatusCodes.Status401Unauthorized, failure?.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_RejectsInactiveTenantBeforeToolExecution()
    {
        var nextInvoked = false;
        var middleware = new McpCallerValidationMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        var context = CreateContext();
        context.Request.Path = "/mcp";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(
            context,
            new TenantContext(),
            new TestTenantStatusChecker(false),
            new TestSessionStatusChecker(UserAccessValidationStatus.Valid));

        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_RejectsRevokedSessionBeforeToolExecution()
    {
        var nextInvoked = false;
        var middleware = new McpCallerValidationMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        var context = CreateContext();
        context.Request.Path = "/mcp";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(
            context,
            new TenantContext(),
            new TestTenantStatusChecker(true),
            new TestSessionStatusChecker(UserAccessValidationStatus.InvalidSession));

        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateContext()
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimConstants.TenantId, TenantId.ToString()));
        identity.AddClaim(new Claim(ClaimConstants.UserId, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(ClaimConstants.SessionId, "session-1"));
        identity.AddClaim(new Claim(ClaimConstants.SecurityStamp, Guid.NewGuid().ToString("N")));
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
    }

    private sealed class TestTenantStatusChecker : ITenantStatusChecker
    {
        private readonly bool _isActive;

        public TestTenantStatusChecker(bool isActive)
        {
            _isActive = isActive;
        }

        public Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_isActive);
        }
    }

    private sealed class TestSessionStatusChecker : IUserSessionStatusChecker
    {
        private readonly UserAccessValidationStatus _status;

        public TestSessionStatusChecker(UserAccessValidationStatus status)
        {
            _status = status;
        }

        public Task<UserAccessValidationStatus> ValidateAccessAsync(
            Guid tenantId,
            Guid userId,
            string sessionId,
            Guid securityStamp,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_status);
        }

        public Task<bool> IsValidForRefreshAsync(
            Guid tenantId,
            Guid userId,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_status == UserAccessValidationStatus.Valid);
        }
    }
}
