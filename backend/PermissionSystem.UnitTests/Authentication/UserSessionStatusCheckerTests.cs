using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Authentication;

public sealed class UserSessionStatusCheckerTests
{
    [Fact]
    public async Task ValidateAccessAsync_ShouldAcceptMatchingActiveUserAndSession()
    {
        await using var fixture = await CreateFixtureAsync();

        var status = await fixture.Checker.ValidateAccessAsync(
            fixture.User.TenantId,
            fixture.User.Id,
            fixture.Session.SessionId,
            fixture.User.SecurityStamp);

        Assert.Equal(UserAccessValidationStatus.Valid, status);
    }

    [Fact]
    public async Task ValidateAccessAsync_ShouldRejectMismatchedSecurityStampAsStale()
    {
        await using var fixture = await CreateFixtureAsync();

        var status = await fixture.Checker.ValidateAccessAsync(
            fixture.User.TenantId,
            fixture.User.Id,
            fixture.Session.SessionId,
            Guid.NewGuid());

        Assert.Equal(UserAccessValidationStatus.StaleAuthorization, status);
    }

    [Fact]
    public async Task ValidateAccessAsync_ShouldRejectDisabledUser()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.User.IsEnabled = false;
        await fixture.DbContext.SaveChangesAsync();

        var status = await fixture.Checker.ValidateAccessAsync(
            fixture.User.TenantId,
            fixture.User.Id,
            fixture.Session.SessionId,
            fixture.User.SecurityStamp);

        Assert.Equal(UserAccessValidationStatus.InactiveUser, status);
    }

    [Fact]
    public async Task ValidateAccessAsync_ShouldRejectRevokedSession()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.Session.IsRevoked = true;
        fixture.Session.RevokedAt = DateTimeOffset.UtcNow;
        await fixture.DbContext.SaveChangesAsync();

        var status = await fixture.Checker.ValidateAccessAsync(
            fixture.User.TenantId,
            fixture.User.Id,
            fixture.Session.SessionId,
            fixture.User.SecurityStamp);

        Assert.Equal(UserAccessValidationStatus.InvalidSession, status);
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Test");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var dbContext = new AppDbContext(options, tenantContext, new NullAuditContext());
        var user = new User
        {
            Id = TestIds.NormalUserId,
            TenantId = TestIds.TenantId,
            UserName = "normal-user",
            NormalizedUserName = "NORMAL-USER",
            DisplayName = "Normal User",
            PasswordHash = "hashed-password",
            IsEnabled = true
        };
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            UserId = user.Id,
            UserName = user.UserName,
            SessionId = "session-1",
            LoginAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        dbContext.Users.Add(user);
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new Fixture(
            dbContext,
            user,
            session,
            new UserSessionStatusChecker(dbContext, new TestCacheService()));
    }

    private sealed record Fixture(
        AppDbContext DbContext,
        User User,
        UserSession Session,
        UserSessionStatusChecker Checker) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return DbContext.DisposeAsync();
        }
    }
}
