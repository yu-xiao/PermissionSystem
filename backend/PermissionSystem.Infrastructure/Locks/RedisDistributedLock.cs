using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure.Options;
using StackExchange.Redis;

namespace PermissionSystem.Infrastructure.Locks;

public sealed class RedisDistributedLock : IDistributedLock
{
    private const string ReleaseScript = """
        if redis.call("GET", KEYS[1]) == ARGV[1] then
            return redis.call("DEL", KEYS[1])
        else
            return 0
        end
        """;

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly LockOptions _options;

    public RedisDistributedLock(IConnectionMultiplexer connectionMultiplexer, LockOptions options)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _options = options;
    }

    public async Task<DistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lockKey = BuildLockKey(key);
        var token = Guid.NewGuid().ToString("N");
        var actualExpiry = GetExpiry(expiry);
        var acquired = await _connectionMultiplexer
            .GetDatabase()
            .StringSetAsync(lockKey, token, actualExpiry, When.NotExists);

        cancellationToken.ThrowIfCancellationRequested();
        return acquired ? new DistributedLockHandle(key, token, actualExpiry) : null;
    }

    public async Task<DistributedLockHandle> AcquireAsync(
        string key,
        TimeSpan? expiry = null,
        TimeSpan? waitTime = null,
        CancellationToken cancellationToken = default)
    {
        var actualWaitTime = GetWaitTime(waitTime);
        var retryDelay = GetRetryDelay();
        var deadline = DateTimeOffset.UtcNow.Add(actualWaitTime);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handle = await TryAcquireAsync(key, expiry, cancellationToken);
            if (handle is not null)
            {
                return handle;
            }

            if (actualWaitTime <= TimeSpan.Zero || DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException($"Distributed lock '{key}' could not be acquired within {actualWaitTime}.");
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            var delay = remaining < retryDelay ? remaining : retryDelay;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public async Task<bool> ReleaseAsync(
        DistributedLockHandle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _connectionMultiplexer
            .GetDatabase()
            .ScriptEvaluateAsync(
                ReleaseScript,
                new RedisKey[] { BuildLockKey(handle.Key) },
                new RedisValue[] { handle.Token });

        cancellationToken.ThrowIfCancellationRequested();
        return (long)result > 0;
    }

    public async Task ExecuteWithLockAsync(
        string key,
        Func<CancellationToken, Task> action,
        TimeSpan? expiry = null,
        TimeSpan? waitTime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await ExecuteWithLockAsync<object?>(
            key,
            async token =>
            {
                await action(token);
                return null;
            },
            expiry,
            waitTime,
            cancellationToken);
    }

    public async Task<TResult> ExecuteWithLockAsync<TResult>(
        string key,
        Func<CancellationToken, Task<TResult>> action,
        TimeSpan? expiry = null,
        TimeSpan? waitTime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var handle = await AcquireAsync(key, expiry, waitTime, cancellationToken);
        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            await ReleaseAsync(handle, CancellationToken.None);
        }
    }

    private string BuildLockKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Distributed lock key cannot be empty.", nameof(key));
        }

        var prefix = string.IsNullOrWhiteSpace(_options.KeyPrefix)
            ? "ps:lock:"
            : _options.KeyPrefix.Trim();

        return $"{prefix}{key.Trim()}";
    }

    private TimeSpan GetExpiry(TimeSpan? expiry)
    {
        var actualExpiry = expiry ?? TimeSpan.FromSeconds(Math.Max(1, _options.DefaultExpirySeconds));
        if (actualExpiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "Distributed lock expiry must be greater than zero.");
        }

        return actualExpiry;
    }

    private TimeSpan GetWaitTime(TimeSpan? waitTime)
    {
        var actualWaitTime = waitTime ?? TimeSpan.FromSeconds(Math.Max(0, _options.DefaultWaitSeconds));
        return actualWaitTime < TimeSpan.Zero ? TimeSpan.Zero : actualWaitTime;
    }

    private TimeSpan GetRetryDelay()
    {
        return TimeSpan.FromMilliseconds(Math.Max(10, _options.RetryDelayMilliseconds));
    }
}
