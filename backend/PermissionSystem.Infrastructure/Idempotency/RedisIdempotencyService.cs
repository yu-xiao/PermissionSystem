using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using StackExchange.Redis;

namespace PermissionSystem.Infrastructure.Idempotency;

public sealed class RedisIdempotencyService : IIdempotencyService
{
    private const string StoreIfOperationMatchesScript = """
        local current = redis.call('GET', KEYS[1])
        if not current then
            return 0
        end

        local currentEntry = cjson.decode(current)
        if currentEntry.operationId ~= ARGV[1] then
            return 0
        end

        redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])
        return 1
        """;

    private const string RemoveIfOperationMatchesScript = """
        local current = redis.call('GET', KEYS[1])
        if not current then
            return 0
        end

        local currentEntry = cjson.decode(current)
        if currentEntry.operationId ~= ARGV[1] then
            return 0
        end

        return redis.call('DEL', KEYS[1])
        """;

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

    public Task<bool> TryBeginAsync(
        string key,
        IdempotencyCacheEntry entry,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var value = JsonSerializer.Serialize(entry, JsonOptions);
        return _connectionMultiplexer
            .GetDatabase()
            .StringSetAsync(BuildIdempotencyKey(key), value, expiresIn, When.NotExists);
    }

    public async Task<bool> StoreAsync(
        string key,
        string operationId,
        IdempotencyCacheEntry entry,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var value = JsonSerializer.Serialize(entry, JsonOptions);
        var result = await _connectionMultiplexer.GetDatabase().ScriptEvaluateAsync(
            StoreIfOperationMatchesScript,
            [BuildIdempotencyKey(key)],
            [operationId, value, Math.Max(1, (long)expiresIn.TotalMilliseconds)]);
        return (int)result == 1;
    }

    public async Task RemoveAsync(
        string key,
        string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(operationId))
        {
            await _connectionMultiplexer.GetDatabase().KeyDeleteAsync(BuildIdempotencyKey(key));
            return;
        }

        await _connectionMultiplexer.GetDatabase().ScriptEvaluateAsync(
            RemoveIfOperationMatchesScript,
            [BuildIdempotencyKey(key)],
            [operationId]);
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
