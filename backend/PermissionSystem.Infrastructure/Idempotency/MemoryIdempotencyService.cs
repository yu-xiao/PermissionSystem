using Microsoft.Extensions.Caching.Memory;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Infrastructure.Idempotency;

public sealed class MemoryIdempotencyService : IIdempotencyService
{
    private readonly IMemoryCache _memoryCache;
    private readonly object _syncRoot = new();

    public MemoryIdempotencyService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task<IdempotencyCacheEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            _memoryCache.TryGetValue(BuildIdempotencyKey(key), out IdempotencyCacheEntry? entry)
                ? entry
                : null);
    }

    public Task<bool> TryBeginAsync(
        string key,
        IdempotencyCacheEntry entry,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cacheKey = BuildIdempotencyKey(key);
        lock (_syncRoot)
        {
            if (_memoryCache.TryGetValue(cacheKey, out _))
            {
                return Task.FromResult(false);
            }

            _memoryCache.Set(cacheKey, entry, expiresIn);
            return Task.FromResult(true);
        }
    }

    public Task<bool> StoreAsync(
        string key,
        string operationId,
        IdempotencyCacheEntry entry,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cacheKey = BuildIdempotencyKey(key);
        lock (_syncRoot)
        {
            if (!_memoryCache.TryGetValue(cacheKey, out IdempotencyCacheEntry? current) ||
                current is null ||
                !string.Equals(current.OperationId, operationId, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            _memoryCache.Set(cacheKey, entry, expiresIn);
            return Task.FromResult(true);
        }
    }

    public Task RemoveAsync(
        string key,
        string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cacheKey = BuildIdempotencyKey(key);
        lock (_syncRoot)
        {
            if (string.IsNullOrEmpty(operationId) ||
                (_memoryCache.TryGetValue(cacheKey, out IdempotencyCacheEntry? current) &&
                 current is not null &&
                 string.Equals(current.OperationId, operationId, StringComparison.Ordinal)))
            {
                _memoryCache.Remove(cacheKey);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryAcquireDuplicateSubmitLockAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cacheKey = BuildDuplicateSubmitKey(key);
        lock (_syncRoot)
        {
            if (_memoryCache.TryGetValue(cacheKey, out _))
            {
                return Task.FromResult(false);
            }

            _memoryCache.Set(cacheKey, true, expiresIn);
            return Task.FromResult(true);
        }
    }

    private static string BuildIdempotencyKey(string key)
    {
        return $"ps:idempotency:{key}";
    }

    private static string BuildDuplicateSubmitKey(string key)
    {
        return $"ps:duplicate-submit:{key}";
    }
}
