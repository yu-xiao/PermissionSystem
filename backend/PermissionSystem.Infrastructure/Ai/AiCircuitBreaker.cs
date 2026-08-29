using System.Collections.Concurrent;
using System.Text.Json;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Abstractions;
using StackExchange.Redis;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiCircuitBreaker : IAiCircuitBreaker
{
    private const int FailureThreshold = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan OpenDuration = TimeSpan.FromMinutes(1);
    private readonly IDistributedLock _distributedLock;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IAiAlertService _alertService;
    private readonly ConcurrentDictionary<string, CircuitState> _local = new(StringComparer.Ordinal);

    public AiCircuitBreaker(IDistributedLock distributedLock, IAiAlertService alertService, IConnectionMultiplexer? redis = null)
    {
        _distributedLock = distributedLock;
        _alertService = alertService;
        _redis = redis;
    }

    public async Task<bool> AllowAsync(AiCircuitTarget target, CancellationToken cancellationToken = default)
    {
        var key = Normalize(target);
        return await UpdateAsync(key, state =>
        {
            var now = DateTimeOffset.UtcNow;
            if (state.OpenUntil.HasValue && state.OpenUntil > now)
            {
                return (state with { HalfOpenClaimed = false }, false);
            }

            if (state.OpenUntil.HasValue)
            {
                if (state.HalfOpenClaimed)
                {
                    return (state, false);
                }

                return (state with { HalfOpenClaimed = true }, true);
            }

            return (state, true);
        }, cancellationToken);
    }

    public async Task RecordSuccessAsync(AiCircuitTarget target, CancellationToken cancellationToken = default)
    {
        var key = Normalize(target);
        var wasOpen = await IsOpenAsync(key, cancellationToken);
        await UpdateAsync(key, _ => (new CircuitState(), true), cancellationToken);
        if (wasOpen)
        {
            await _alertService.NotifyCircuitRecoveredAsync(target, CancellationToken.None);
        }
    }

    public Task RecordFailureAsync(AiCircuitTarget target, string errorCode, CancellationToken cancellationToken = default)
    {
        var key = Normalize(target);
        return UpdateAsync(key, state =>
        {
            var now = DateTimeOffset.UtcNow;
            var failures = state.FailureWindowStart.HasValue && now - state.FailureWindowStart < FailureWindow
                ? state.Failures + 1
                : 1;
            var opened = failures >= FailureThreshold ? now.Add(OpenDuration) : state.OpenUntil;
            return (new CircuitState(failures, now, opened, false, errorCode), opened.HasValue);
        }, cancellationToken);
    }

    private async Task<bool> UpdateAsync(string key, Func<CircuitState, (CircuitState State, bool Result)> update, CancellationToken cancellationToken)
    {
        if (_redis is null)
        {
            var current = _local.GetOrAdd(key, _ => new CircuitState());
            var next = update(current);
            _local[key] = next.State;
            if (next.State.OpenUntil.HasValue && !current.OpenUntil.HasValue)
            {
                await _alertService.NotifyCircuitOpenedAsync(ParseTarget(key), next.State.LastErrorCode ?? "unknown", CancellationToken.None);
            }
            return next.Result;
        }

        return await _distributedLock.ExecuteWithLockAsync(
            $"ai:circuit:{key}",
            async token =>
            {
                var db = _redis.GetDatabase();
                var redisKey = (RedisKey)$"ps:ai-circuit:{key}";
                var raw = await db.StringGetAsync(redisKey);
                var state = raw.HasValue
                    ? JsonSerializer.Deserialize<CircuitState>(raw.ToString()) ?? new CircuitState()
                    : new CircuitState();
                var next = update(state);
                await db.StringSetAsync(redisKey, JsonSerializer.Serialize(next.State), TimeSpan.FromMinutes(3));
                if (next.State.OpenUntil.HasValue && !state.OpenUntil.HasValue)
                {
                    await _alertService.NotifyCircuitOpenedAsync(ParseTarget(key), next.State.LastErrorCode ?? "unknown", CancellationToken.None);
                }
                return next.Result;
            },
            expiry: TimeSpan.FromSeconds(10),
            waitTime: TimeSpan.FromSeconds(2),
            cancellationToken: cancellationToken);
    }

    private async Task<bool> IsOpenAsync(string key, CancellationToken cancellationToken)
    {
        if (_redis is null)
        {
            return _local.TryGetValue(key, out var state) && state.OpenUntil.HasValue;
        }

        var raw = await _redis.GetDatabase().StringGetAsync((RedisKey)$"ps:ai-circuit:{key}");
        if (!raw.HasValue) return false;
        var parsedState = JsonSerializer.Deserialize<CircuitState>(raw.ToString());
        return parsedState?.OpenUntil.HasValue == true;
    }

    private static AiCircuitTarget ParseTarget(string key)
    {
        var index = key.IndexOf(':');
        return index < 0 ? new AiCircuitTarget("unknown", key) : new AiCircuitTarget(key[..index], key[(index + 1)..]);
    }

    private static string Normalize(AiCircuitTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.Kind) || string.IsNullOrWhiteSpace(target.Key))
        {
            throw new ArgumentException("Circuit target is required.", nameof(target));
        }

        return $"{target.Kind.Trim().ToLowerInvariant()}:{target.Key.Trim().ToLowerInvariant()}";
    }

    private sealed record CircuitState(
        int Failures = 0,
        DateTimeOffset? FailureWindowStart = null,
        DateTimeOffset? OpenUntil = null,
        bool HalfOpenClaimed = false,
        string? LastErrorCode = null);
}
