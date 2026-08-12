namespace PermissionSystem.Application.Abstractions;

public interface IDistributedRateLimitService
{
    Task<RateLimitAcquireResult> TryAcquireAsync(
        string policyName,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}

public sealed record RateLimitAcquireResult(bool IsAcquired, TimeSpan RetryAfter)
{
    public static RateLimitAcquireResult Acquired { get; } = new(true, TimeSpan.Zero);
}
