using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using StackExchange.Redis;

namespace PermissionSystem.Infrastructure.Idempotency;

public sealed class RedisIdempotencyService : IIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisIdempotencyService(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<IdempotencyCacheEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await _connectionMultiplexer.GetDatabase().StringGetAsync(BuildIdempotencyKey(key));
        return value.HasValue
            ? JsonSerializer.Deserialize<IdempotencyCacheEntry>(value.ToString(), JsonOptions)
            : null;
    }

    public Task<bool> TryBeginAsync(string key, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        var entry = JsonSerializer.Serialize(new IdempotencyCacheEntry { State = "Processing" }, JsonOptions);
        return _connectionMultiplexer
            .GetDatabase()
            .StringSetAsync(BuildIdempotencyKey(key), entry, expiresIn, When.NotExists);
    }

    public Task StoreAsync(
        string key,
        IdempotencyCacheEntry entry,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var value = JsonSerializer.Serialize(entry, JsonOptions);
        return _connectionMultiplexer.GetDatabase().StringSetAsync(BuildIdempotencyKey(key), value, expiresIn);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return _connectionMultiplexer.GetDatabase().KeyDeleteAsync(BuildIdempotencyKey(key));
    }

    public Task<bool> TryAcquireDuplicateSubmitLockAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        return _connectionMultiplexer
            .GetDatabase()
            .StringSetAsync(BuildDuplicateSubmitKey(key), "1", expiresIn, When.NotExists);
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
