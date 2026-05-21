namespace PermissionSystem.Application.Abstractions;

public interface IDistributedLock
{
    Task<DistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);

    Task<DistributedLockHandle> AcquireAsync(
        string key,
        TimeSpan? expiry = null,
        TimeSpan? waitTime = null,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseAsync(
        DistributedLockHandle handle,
        CancellationToken cancellationToken = default);

    Task ExecuteWithLockAsync(
        string key,
        Func<CancellationToken, Task> action,
        TimeSpan? expiry = null,
        TimeSpan? waitTime = null,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteWithLockAsync<TResult>(
        string key,
        Func<CancellationToken, Task<TResult>> action,
        TimeSpan? expiry = null,
        TimeSpan? waitTime = null,
        CancellationToken cancellationToken = default);
}

public sealed class DistributedLockHandle
{
    public DistributedLockHandle(string key, string token, TimeSpan expiry)
    {
        Key = key;
        Token = token;
        Expiry = expiry;
        AcquiredAt = DateTimeOffset.UtcNow;
    }

    public string Key { get; }

    public string Token { get; }

    public TimeSpan Expiry { get; }

    public DateTimeOffset AcquiredAt { get; }
}
