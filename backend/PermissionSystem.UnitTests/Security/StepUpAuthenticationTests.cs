using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Security;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Security;

public sealed class StepUpAuthenticationTests
{
    [Fact]
    public void TicketProvider_ShouldIgnoreLegacyQueryStringAndHeader()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?verificationCode=query-code");
        httpContext.Request.Headers["X-Sensitive-Verification-Code"] = "legacy-code";
        var provider = new SensitiveOperationCodeProvider(new HttpContextAccessor { HttpContext = httpContext });

        Assert.Null(provider.StepUpTicket);

        httpContext.Request.Headers["X-Step-Up-Ticket"] = "step-up-ticket";
        Assert.Equal("step-up-ticket", provider.StepUpTicket);
    }

    [Fact]
    public async Task CreateChallenge_ShouldPersistNoSecretAndBindSession()
    {
        var currentUser = new TestCurrentUserService { UserId = TestIds.NormalUserId };
        var verifications = new InMemoryRepository<SensitiveOperationVerification>();
        var service = CreateService(
            currentUser,
            verifications,
            new User { Id = TestIds.NormalUserId, TenantId = TestIds.TenantId, IsEnabled = true, PasswordHash = "correct" });

        var response = await service.SendVerificationAsync(new SendSensitiveVerificationRequest
        {
            OperationCode = "security:test"
        });

        var challenge = Assert.Single(verifications.Items);
        Assert.Equal(response.ChallengeId, challenge.Id);
        Assert.Equal("test-session", challenge.SessionId);
        Assert.Null(challenge.TicketHash);
        Assert.Null(challenge.UsedAt);
    }

    [Fact]
    public async Task VerifyAndConsume_ShouldIssueShortLivedOneTimeTicket()
    {
        var currentUser = new TestCurrentUserService { UserId = TestIds.NormalUserId };
        var verifications = new InMemoryRepository<SensitiveOperationVerification>();
        var store = new TestStepUpVerificationStore(verifications);
        var provider = new TestSensitiveOperationCodeProvider();
        var service = CreateService(
            currentUser,
            verifications,
            new User { Id = TestIds.NormalUserId, TenantId = TestIds.TenantId, IsEnabled = true, PasswordHash = "correct" },
            store,
            provider);

        var challenge = await service.SendVerificationAsync(new SendSensitiveVerificationRequest
        {
            OperationCode = "security:test"
        });
        var ticket = await service.VerifyAsync(new VerifySensitiveOperationRequest
        {
            ChallengeId = challenge.ChallengeId,
            Password = "correct"
        });
        provider.StepUpTicket = ticket.StepUpTicket;

        await service.EnsureSensitiveOperationVerifiedAsync("security:test", force: true);
        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.EnsureSensitiveOperationVerifiedAsync("security:test", force: true));

        Assert.NotEmpty(ticket.StepUpTicket);
        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Single(verifications.Items, entity => entity.UsedAt.HasValue);
    }

    [Fact]
    public async Task Ticket_ShouldRejectDifferentUserTenantSessionAndOperation()
    {
        var currentUser = new TestCurrentUserService { UserId = TestIds.NormalUserId };
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Test");
        var provider = new TestSensitiveOperationCodeProvider();
        var verifications = new InMemoryRepository<SensitiveOperationVerification>();
        var service = CreateService(
            currentUser,
            verifications,
            new User { Id = TestIds.NormalUserId, TenantId = TestIds.TenantId, IsEnabled = true, PasswordHash = "correct" },
            provider: provider,
            tenantContext: tenantContext);
        var challenge = await service.SendVerificationAsync(new SendSensitiveVerificationRequest
        {
            OperationCode = "security:test"
        });
        var ticket = await service.VerifyAsync(new VerifySensitiveOperationRequest
        {
            ChallengeId = challenge.ChallengeId,
            Password = "correct"
        });
        provider.StepUpTicket = ticket.StepUpTicket;

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.EnsureSensitiveOperationVerifiedAsync("security:other", force: true));

        currentUser.SessionId = "other-session";
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.EnsureSensitiveOperationVerifiedAsync("security:test", force: true));

        currentUser.SessionId = "test-session";
        currentUser.UserId = TestIds.ApproverUserId;
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.EnsureSensitiveOperationVerifiedAsync("security:test", force: true));

        currentUser.UserId = TestIds.NormalUserId;
        tenantContext.SetTenant(Guid.NewGuid(), "Test");
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.EnsureSensitiveOperationVerifiedAsync("security:test", force: true));
    }

    [Fact]
    public async Task Verify_ShouldRejectExpiredChallenge()
    {
        var currentUser = new TestCurrentUserService { UserId = TestIds.NormalUserId };
        var verifications = new InMemoryRepository<SensitiveOperationVerification>();
        var service = CreateService(
            currentUser,
            verifications,
            new User { Id = TestIds.NormalUserId, TenantId = TestIds.TenantId, IsEnabled = true, PasswordHash = "correct" });
        var challenge = await service.SendVerificationAsync(new SendSensitiveVerificationRequest
        {
            OperationCode = "security:test"
        });
        verifications.Items.Single().ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.VerifyAsync(new VerifySensitiveOperationRequest
        {
            ChallengeId = challenge.ChallengeId,
            Password = "correct"
        }));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task VerifyWithWrongPassword_ShouldLockAfterFiveAttempts()
    {
        var currentUser = new TestCurrentUserService { UserId = TestIds.NormalUserId };
        var verifications = new InMemoryRepository<SensitiveOperationVerification>();
        var service = CreateService(
            currentUser,
            verifications,
            new User { Id = TestIds.NormalUserId, TenantId = TestIds.TenantId, IsEnabled = true, PasswordHash = "correct" });
        var challenge = await service.SendVerificationAsync(new SendSensitiveVerificationRequest
        {
            OperationCode = "security:test"
        });

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<BusinessException>(() => service.VerifyAsync(new VerifySensitiveOperationRequest
            {
                ChallengeId = challenge.ChallengeId,
                Password = "wrong"
            }));
        }

        Assert.NotNull(verifications.Items.Single().LockedAt);
        await Assert.ThrowsAsync<BusinessException>(() => service.VerifyAsync(new VerifySensitiveOperationRequest
        {
            ChallengeId = challenge.ChallengeId,
            Password = "correct"
        }));
    }

    private static SecurityPolicyService CreateService(
        TestCurrentUserService currentUser,
        InMemoryRepository<SensitiveOperationVerification> verifications,
        User user,
        TestStepUpVerificationStore? store = null,
        TestSensitiveOperationCodeProvider? provider = null,
        TenantContext? tenantContext = null)
    {
        tenantContext ??= new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Test");
        var users = new InMemoryRepository<User>(user);
        return new SecurityPolicyService(
            new InMemoryRepository<SecurityPolicy>(),
            new InMemoryRepository<LoginFailureRecord>(),
            verifications,
            users,
            new InMemoryRepository<IpAccessRule>(),
            tenantContext,
            currentUser,
            provider ?? new TestSensitiveOperationCodeProvider(),
            new TestPasswordHashService(),
            store ?? new TestStepUpVerificationStore(verifications),
            NullLogger<SecurityPolicyService>.Instance,
            new TestUnitOfWork());
    }

    private sealed class TestSensitiveOperationCodeProvider : ISensitiveOperationCodeProvider
    {
        public string? StepUpTicket { get; set; }
    }

    private sealed class TestPasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => password;

        public bool VerifyPassword(string passwordHash, string password) => passwordHash == password;
    }

    private sealed class TestStepUpVerificationStore : IStepUpVerificationStore
    {
        private readonly InMemoryRepository<SensitiveOperationVerification> _repository;

        public TestStepUpVerificationStore(InMemoryRepository<SensitiveOperationVerification> repository)
        {
            _repository = repository;
        }

        public Task<bool> RegisterFailedAttemptAsync(Guid id, int maxAttempts, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            var entity = _repository.Items.SingleOrDefault(item => item.Id == id && item.LockedAt == null && item.VerifiedAt == null);
            if (entity is null)
            {
                return Task.FromResult(false);
            }

            entity.FailedAttemptCount++;
            if (entity.FailedAttemptCount >= maxAttempts)
            {
                entity.LockedAt = now;
            }

            return Task.FromResult(true);
        }

        public Task<bool> MarkVerifiedAsync(Guid id, string ticketHash, DateTimeOffset verifiedAt, DateTimeOffset ticketExpiresAt, CancellationToken cancellationToken = default)
        {
            var entity = _repository.Items.SingleOrDefault(item => item.Id == id && item.LockedAt == null && item.VerifiedAt == null);
            if (entity is null)
            {
                return Task.FromResult(false);
            }

            entity.TicketHash = ticketHash;
            entity.VerifiedAt = verifiedAt;
            entity.TicketExpiresAt = ticketExpiresAt;
            return Task.FromResult(true);
        }

        public Task<bool> TryConsumeTicketAsync(Guid tenantId, Guid userId, string sessionId, string operationCode, string ticketHash, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            var entity = _repository.Items.SingleOrDefault(item => item.TenantId == tenantId && item.UserId == userId && item.SessionId == sessionId && item.OperationCode == operationCode && item.TicketHash == ticketHash && item.UsedAt == null && item.TicketExpiresAt > now);
            if (entity is null)
            {
                return Task.FromResult(false);
            }

            entity.UsedAt = now;
            return Task.FromResult(true);
        }
    }
}
