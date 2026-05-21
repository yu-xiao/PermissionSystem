using System.Collections.Concurrent;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.Infrastructure.Locks;

public sealed class MemoryDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, LockEntry> _locks = new();
    private readonly LockOptions _options;

    public MemoryDistributedLock(LockOptions options)
    {
        _options = options;
    }

    public Task<DistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lockKey = BuildLockKey(key);
        var actualExpiry = GetExpiry(expiry);
        var token = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.Add(actualExpiry);

        while (true)
        {
            if (_locks.TryGetValue(lockKey, out var existing) && existing.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                _locks.TryRemove(new KeyValuePair<string, LockEntry>(lockKey, existing));
                continue;
            }

            var entry = new LockEntry(token, expiresAt);
            if (_locks.TryAdd(lockKey, entry))
            {
                return Task.FromResult<DistributedLockHandle?>(
                    new DistributedLockHandle(key, token, actualExpiry));
            }

            return Task.FromResult<DistributedLockHandle?>(null);
        }
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
                throw new TimeoutException($"Local lock '{key}' could not be acquired within {actualWaitTime}.");
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            var delay = remaining < retryDelay ? remaining : retryDelay;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public Task<bool> ReleaseAsync(
        DistributedLockHandle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        cancellationToken.ThrowIfCancellationRequested();

        var lockKey = BuildLockKey(handle.Key);
        var released = _locks.TryGetValue(lockKey, out var existing) &&
            existing.Token == handle.Token &&
            _locks.TryRemove(new KeyValuePair<string, LockEntry>(lockKey, existing));

        return Task.FromResult(released);
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
            throw new ArgumentException("Lock key cannot be empty.", nameof(key));
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
            throw new ArgumentOutOfRangeException(nameof(expiry), "Lock expiry must be greater than zero.");
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

    private sealed record LockEntry(string Token, DateTimeOffset ExpiresAt);
}
