using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiRunAdmissionServiceTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData(100, null, false)]
    [InlineData(100, 100, true)]
    public async Task ExecuteAsync_UsesEstimatesOnlyForMissingUsage(int? input, int? output, bool admitted)
    {
        var run = new AiRun { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, ActorUserId = TestIds.NormalUserId };
        var usage = new AiUsageLog
        {
            TenantId = TestIds.TenantId, RunId = run.Id, CreatedAt = DateTimeOffset.UtcNow,
            InputTokens = input, OutputTokens = output, EstimatedInputTokens = 500, EstimatedOutputTokens = 1000
        };
        var service = new AiRunAdmissionService(new InMemoryRepository<AiRun>(run),
            new InMemoryRepository<AiUsageLog>(usage), new InMemoryAsyncQueryExecutor(),
            new AllowRateLimit(), new TestDistributedLock(), new AiCenterOptions { TokenLimitPerHour = 1000 });
        var called = false;
        async Task<bool> Execute() => await service.ExecuteAsync(
            new AiRunAdmissionRequest(TestIds.TenantId, TestIds.NormalUserId, "agent", Guid.NewGuid(), 1),
            () => { called = true; return Task.FromResult(true); });

        if (admitted) Assert.True(await Execute());
        else Assert.Equal(ErrorCode.TooManyRequests, (await Assert.ThrowsAsync<BusinessException>(Execute)).ErrorCode);
        Assert.Equal(admitted, called);
    }

    private sealed class AllowRateLimit : IDistributedRateLimitService
    {
        public Task<RateLimitAcquireResult> TryAcquireAsync(string policyName, string partitionKey,
            int permitLimit, TimeSpan window, CancellationToken cancellationToken = default) =>
            Task.FromResult(RateLimitAcquireResult.Acquired);
    }
}
