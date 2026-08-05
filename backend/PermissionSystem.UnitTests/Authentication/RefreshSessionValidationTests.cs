using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Authentication;

public sealed class RefreshSessionValidationTests
{
    [Fact]
    public async Task ValidSession_ShouldBeAccepted_RegardlessOfCurrentRequestTenant()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = CreateSession(tenantId, userId, "valid-session");
        var currentRequestTenantId = Guid.NewGuid();
        await using var fixture = CreateFixture(currentRequestTenantId, session);
        await fixture.SaveAsync();

        var result = await fixture.Checker.IsValidForRefreshAsync(
            tenantId,
            userId,
            session.SessionId);

        Assert.NotEqual(tenantId, currentRequestTenantId);
        Assert.True(result);
    }

    [Fact]
    public async Task SessionFromAnotherTenant_ShouldBeRejected()
    {
        var session = CreateSession(Guid.NewGuid(), Guid.NewGuid(), "cross-tenant-session");
        await using var fixture = CreateFixture(session);
        await fixture.SaveAsync();

        var result = await fixture.Checker.IsValidForRefreshAsync(
            Guid.NewGuid(),
            session.UserId,
            session.SessionId);

        Assert.False(result);
    }

    [Fact]
    public async Task SessionFromAnotherUser_ShouldBeRejected()
    {
        var session = CreateSession(Guid.NewGuid(), Guid.NewGuid(), "cross-user-session");
        await using var fixture = CreateFixture(session);
        await fixture.SaveAsync();

        var result = await fixture.Checker.IsValidForRefreshAsync(
            session.TenantId,
            Guid.NewGuid(),
            session.SessionId);

        Assert.False(result);
    }

    [Fact]
    public async Task MissingSession_ShouldBeRejected()
    {
        await using var fixture = CreateFixture();

        var result = await fixture.Checker.IsValidForRefreshAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "missing-session");

        Assert.False(result);
    }

    [Fact]
    public async Task RevokedSession_ShouldBeRejected()
    {
        var session = CreateSession(Guid.NewGuid(), Guid.NewGuid(), "revoked-session");
        session.IsRevoked = true;
        await using var fixture = CreateFixture(session);
        await fixture.SaveAsync();

        var result = await fixture.Checker.IsValidForRefreshAsync(
            session.TenantId,
            session.UserId,
            session.SessionId);

        Assert.False(result);
    }

    [Fact]
    public async Task ExpiredSession_ShouldBeRejected()
    {
        var session = CreateSession(Guid.NewGuid(), Guid.NewGuid(), "expired-session");
        session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await using var fixture = CreateFixture(session);
        await fixture.SaveAsync();

        var result = await fixture.Checker.IsValidForRefreshAsync(
            session.TenantId,
            session.UserId,
            session.SessionId);

        Assert.False(result);
    }

    [Fact]
    public async Task DeletedSession_ShouldBeRejected()
    {
        var session = CreateSession(Guid.NewGuid(), Guid.NewGuid(), "deleted-session");
        await using var fixture = CreateFixture(session);
        await fixture.SaveAsync();
        session.IsDeleted = true;
        await fixture.SaveAsync();

        var result = await fixture.Checker.IsValidForRefreshAsync(
            session.TenantId,
            session.UserId,
            session.SessionId);

        Assert.False(result);
    }

    [Fact]
    public async Task RevokedSessionInCache_ShouldBeRejected()
    {
        var session = CreateSession(Guid.NewGuid(), Guid.NewGuid(), "cached-revoked-session");
        var cache = new TestCacheService();
        await cache.SetAsync(UserSessionCacheKeys.Revoked(session.SessionId), true);
        await using var fixture = CreateFixture(cache, session);
        await fixture.SaveAsync();

        var result = await fixture.Checker.IsValidForRefreshAsync(
            session.TenantId,
            session.UserId,
            session.SessionId);

        Assert.False(result);
    }

    [Fact]
    public async Task Cancellation_ShouldBeObserved()
    {
        var session = CreateSession(Guid.NewGuid(), Guid.NewGuid(), "cancelled-session");
        await using var fixture = CreateFixture(session);
        await fixture.SaveAsync();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Checker.IsValidForRefreshAsync(
                session.TenantId,
                session.UserId,
                session.SessionId,
                cancellationSource.Token));
    }

    private static TestFixture CreateFixture(params UserSession[] sessions)
    {
        return CreateFixture(new TestCacheService(), sessions);
    }

    private static TestFixture CreateFixture(TestCacheService cache, params UserSession[] sessions)
    {
        return CreateFixture(Guid.NewGuid(), cache, sessions);
    }

    private static TestFixture CreateFixture(Guid currentTenantId, params UserSession[] sessions)
    {
        return CreateFixture(currentTenantId, new TestCacheService(), sessions);
    }

    private static TestFixture CreateFixture(
        Guid currentTenantId,
        TestCacheService cache,
        params UserSession[] sessions)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(currentTenantId, "RefreshSessionValidationTest");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var dbContext = new AppDbContext(options, tenantContext, new NullAuditContext());
        dbContext.UserSessions.AddRange(sessions);
        var systemTenantScope = new SystemTenantScope(
            tenantContext,
            NullLogger<SystemTenantScope>.Instance);

        return new TestFixture(
            dbContext,
            systemTenantScope,
            new UserSessionStatusChecker(dbContext, cache));
    }

    private static UserSession CreateSession(Guid tenantId, Guid userId, string sessionId)
    {
        return new UserSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            UserName = "test-user",
            SessionId = sessionId,
            LoginAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SystemTenantScope _systemTenantScope;

        public TestFixture(
            AppDbContext dbContext,
            SystemTenantScope systemTenantScope,
            IUserSessionStatusChecker checker)
        {
            DbContext = dbContext;
            _systemTenantScope = systemTenantScope;
            Checker = checker;
        }

        public AppDbContext DbContext { get; }

        public IUserSessionStatusChecker Checker { get; }

        public async Task SaveAsync()
        {
            using (_systemTenantScope.Begin("RefreshSessionValidationTestDataSetup"))
            {
                await DbContext.SaveChangesAsync();
            }
        }

        public ValueTask DisposeAsync()
        {
            return DbContext.DisposeAsync();
        }
    }
}
