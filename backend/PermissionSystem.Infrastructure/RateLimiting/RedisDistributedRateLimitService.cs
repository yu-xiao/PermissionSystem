using PermissionSystem.Application.Abstractions;
using StackExchange.Redis;

namespace PermissionSystem.Infrastructure.RateLimiting;

public sealed class RedisDistributedRateLimitService : IDistributedRateLimitService
{
    private const string IncrementWithinFixedWindowScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end

        local ttl = redis.call('PTTL', KEYS[1])
        if count > tonumber(ARGV[2]) then
            return {0, ttl}
        end

        return {1, ttl}
        """;

    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisDistributedRateLimitService(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<RateLimitAcquireResult> TryAcquireAsync(
        string policyName,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (permitLimit <= 0)
        {
            return RateLimitAcquireResult.Acquired;
        }

        var normalizedWindow = window <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : window;
        var now = DateTimeOffset.UtcNow;
        var windowStart = GetWindowStart(now, normalizedWindow);
        var windowEnd = windowStart.Add(normalizedWindow);
        var key = $"ps:rate-limit:{policyName}:{partitionKey}:{windowStart.ToUnixTimeMilliseconds()}";
        var scriptResult = await _connectionMultiplexer.GetDatabase().ScriptEvaluateAsync(
            IncrementWithinFixedWindowScript,
            [key],
            [Math.Max(1, (long)(windowEnd - now).TotalMilliseconds), Math.Max(1, permitLimit)]);
        if (scriptResult.IsNull || scriptResult.Resp2Type != ResultType.Array)
        {
            throw new InvalidOperationException("Redis rate limit script returned an invalid result.");
        }

        var result = (RedisResult[]?)scriptResult ?? throw new InvalidOperationException(
            "Redis rate limit script returned an invalid result.");
        var isAcquired = (int)result[0] == 1;
        var retryAfter = TimeSpan.FromMilliseconds(Math.Max(0, (long)result[1]));
        return isAcquired
            ? RateLimitAcquireResult.Acquired
            : new RateLimitAcquireResult(false, retryAfter);
    }

    private static DateTimeOffset GetWindowStart(DateTimeOffset now, TimeSpan window)
    {
        var windowMilliseconds = (long)window.TotalMilliseconds;
        var windowStartMilliseconds = now.ToUnixTimeMilliseconds() / windowMilliseconds * windowMilliseconds;
        return DateTimeOffset.FromUnixTimeMilliseconds(windowStartMilliseconds);
    }
}
