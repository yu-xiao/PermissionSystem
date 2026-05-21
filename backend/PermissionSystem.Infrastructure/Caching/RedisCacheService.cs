using System.Text.Json;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure.Options;
using StackExchange.Redis;

namespace PermissionSystem.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly CacheOptions _options;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer, IOptions<CacheOptions> options)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _options = options.Value;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var value = await _connectionMultiplexer.GetDatabase().StringGetAsync(BuildKey(key));

        return !value.HasValue
            ? default
            : JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _connectionMultiplexer.GetDatabase().KeyExistsAsync(BuildKey(key));
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(value, JsonOptions);
        await _connectionMultiplexer
            .GetDatabase()
            .StringSetAsync(BuildKey(key), payload, GetExpiration(absoluteExpirationRelativeToNow, slidingExpiration));
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var cachedValue = await GetAsync<T>(key, cancellationToken);
        if (cachedValue is not null)
        {
            return cachedValue;
        }

        var value = await factory(cancellationToken);
        await SetAsync(key, value, absoluteExpirationRelativeToNow, slidingExpiration, cancellationToken);

        return value;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _connectionMultiplexer.GetDatabase().KeyDeleteAsync(BuildKey(key));
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pattern = $"{BuildKey(prefix)}*";
        var database = _connectionMultiplexer.GetDatabase();

        foreach (var endpoint in _connectionMultiplexer.GetEndPoints())
        {
            var server = _connectionMultiplexer.GetServer(endpoint);
            if (!server.IsConnected)
            {
                continue;
            }

            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await database.KeyDeleteAsync(key);
            }
        }
    }

    public Task RefreshAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private TimeSpan? GetExpiration(TimeSpan? absoluteExpirationRelativeToNow, TimeSpan? slidingExpiration)
    {
        if (absoluteExpirationRelativeToNow.HasValue)
        {
            return absoluteExpirationRelativeToNow;
        }

        if (slidingExpiration.HasValue)
        {
            return slidingExpiration;
        }

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
