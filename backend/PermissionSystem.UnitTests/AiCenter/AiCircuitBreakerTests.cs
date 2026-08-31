using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiCircuitBreakerTests
{
    [Fact]
    public async Task RecordFailureAsync_OpensAfterThresholdAndNotifiesOnce()
    {
        var alerts = new RecordingAlertService();
        var circuitBreaker = new AiCircuitBreaker(new TestDistributedLock(), alerts);
        var target = new AiCircuitTarget(
            "mcp-dataset",
            $"{Guid.NewGuid():N}:department-directory");

        for (var index = 0; index < 5; index++)
        {
            await circuitBreaker.RecordFailureAsync(target, "InternalServerError");
        }

        Assert.False(await circuitBreaker.AllowAsync(target));
        var alert = Assert.Single(alerts.Opened);
        Assert.Equal(target, alert.Target);
        Assert.Equal("InternalServerError", alert.ErrorCode);
    }

    [Fact]
    public async Task RecordSuccessAsync_ClearsFailuresBeforeThreshold()
    {
        var circuitBreaker = new AiCircuitBreaker(
            new TestDistributedLock(),
            new RecordingAlertService());
        var target = new AiCircuitTarget(
            "mcp-dataset",
            $"{Guid.NewGuid():N}:platform-capabilities");

        for (var index = 0; index < 4; index++)
        {
            await circuitBreaker.RecordFailureAsync(target, "InternalServerError");
        }

        await circuitBreaker.RecordSuccessAsync(target);
        await circuitBreaker.RecordFailureAsync(target, "InternalServerError");

        Assert.True(await circuitBreaker.AllowAsync(target));
    }

    private sealed class RecordingAlertService : IAiAlertService
    {
        public List<(AiCircuitTarget Target, string ErrorCode)> Opened { get; } = [];

        public Task NotifyCircuitOpenedAsync(
            AiCircuitTarget target,
            string errorCode,
            CancellationToken cancellationToken = default)
        {
            Opened.Add((target, errorCode));
            return Task.CompletedTask;
        }
    }
}
