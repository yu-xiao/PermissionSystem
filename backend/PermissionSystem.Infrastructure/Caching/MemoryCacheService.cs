using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.Infrastructure.Caching;

public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly CacheOptions _options;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    public MemoryCacheService(IMemoryCache memoryCache, IOptions<CacheOptions> options)
    {
        _memoryCache = memoryCache;
        _options = options.Value;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_memoryCache.TryGetValue(BuildKey(key), out T? value) ? value : default);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_memoryCache.TryGetValue(BuildKey(key), out _));
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cacheKey = BuildKey(key);
        var cacheEntryOptions = CreateEntryOptions(cacheKey, absoluteExpirationRelativeToNow, slidingExpiration);
        _memoryCache.Set(cacheKey, value, cacheEntryOptions);
        _keys[cacheKey] = 0;

        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var cacheKey = BuildKey(key);
        if (_memoryCache.TryGetValue(cacheKey, out T? cachedValue) && cachedValue is not null)
        {
            return cachedValue;
        }

        var keyLock = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out cachedValue) && cachedValue is not null)
            {
                return cachedValue;
            }

            var value = await factory(cancellationToken);
            var cacheEntryOptions = CreateEntryOptions(cacheKey, absoluteExpirationRelativeToNow, slidingExpiration);
            _memoryCache.Set(cacheKey, value, cacheEntryOptions);
            _keys[cacheKey] = 0;

            return value;
        }
        finally
        {
            keyLock.Release();
        }
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cacheKey = BuildKey(key);
        _memoryCache.Remove(cacheKey);
        _keys.TryRemove(cacheKey, out _);

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var actualPrefix = BuildKey(prefix);
        foreach (var key in _keys.Keys.Where(key => key.StartsWith(actualPrefix, StringComparison.Ordinal)).ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            _memoryCache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task RefreshAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private MemoryCacheEntryOptions CreateEntryOptions(
        string cacheKey,
        TimeSpan? absoluteExpirationRelativeToNow,
        TimeSpan? slidingExpiration)
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow ?? GetDefaultExpiration(),
            SlidingExpiration = slidingExpiration
        };

        cacheEntryOptions.RegisterPostEvictionCallback((key, _, _, _) =>
        {
            if (key is string removedKey)
            {
                _keys.TryRemove(removedKey, out _);
                _keyLocks.TryRemove(removedKey, out _);
            }
        });

        _keys[cacheKey] = 0;
        return cacheEntryOptions;
    }

    private TimeSpan? GetDefaultExpiration()
    {
        return _options.DefaultExpirationMinutes > 0
            ? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes)
            : null;
    }

    private string BuildKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be empty.", nameof(key));
        }

        var normalizedKey = key.Trim();
        var prefix = _options.KeyPrefix?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(prefix) ||
            normalizedKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return normalizedKey;
        }

        return $"{prefix}{normalizedKey}";
    }
}
