using System.Collections.Concurrent;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Infrastructure.RateLimiting;

public sealed class MemoryDistributedRateLimitService : IDistributedRateLimitService
{
    private readonly ConcurrentDictionary<string, RateLimitWindow> _windows = new();
    private readonly object _syncRoot = new();

    public Task<RateLimitAcquireResult> TryAcquireAsync(
        string policyName,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (permitLimit <= 0)
        {
            return Task.FromResult(RateLimitAcquireResult.Acquired);
        }

        var normalizedWindow = NormalizeWindow(window);
        var now = DateTimeOffset.UtcNow;
        var windowStart = GetWindowStart(now, normalizedWindow);
        var retryAfter = windowStart.Add(normalizedWindow) - now;
        var key = $"{policyName}:{partitionKey}:{windowStart.ToUnixTimeMilliseconds()}";

        lock (_syncRoot)
        {
            var count = _windows.AddOrUpdate(
                key,
                _ => new RateLimitWindow(1, windowStart.Add(normalizedWindow)),
                (_, current) => current with { Count = current.Count + 1 }).Count;
            foreach (var expired in _windows
                         .Where(item => item.Value.ExpiresAtUtc <= now)
                         .Select(item => item.Key)
                         .ToArray())
            {
                _windows.TryRemove(expired, out _);
            }

            return Task.FromResult(count <= Math.Max(1, permitLimit)
                ? RateLimitAcquireResult.Acquired
                : new RateLimitAcquireResult(false, retryAfter));
        }
    }

    private static DateTimeOffset GetWindowStart(DateTimeOffset now, TimeSpan window)
    {
        var windowMilliseconds = (long)window.TotalMilliseconds;
        var windowStartMilliseconds = now.ToUnixTimeMilliseconds() / windowMilliseconds * windowMilliseconds;
        return DateTimeOffset.FromUnixTimeMilliseconds(windowStartMilliseconds);
    }

    private static TimeSpan NormalizeWindow(TimeSpan window)
    {
        return window <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : window;
    }

    private sealed record RateLimitWindow(int Count, DateTimeOffset ExpiresAtUtc);
}
