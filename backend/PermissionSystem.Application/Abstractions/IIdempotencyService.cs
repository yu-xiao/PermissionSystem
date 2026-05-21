namespace PermissionSystem.Application.Abstractions;

public interface IIdempotencyService
{
    Task<IdempotencyCacheEntry?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> TryBeginAsync(string key, TimeSpan expiresIn, CancellationToken cancellationToken = default);

    Task StoreAsync(string key, IdempotencyCacheEntry entry, TimeSpan expiresIn, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> TryAcquireDuplicateSubmitLockAsync(string key, TimeSpan expiresIn, CancellationToken cancellationToken = default);
}

public sealed class IdempotencyCacheEntry
{
    public string State { get; init; } = "Completed";

    public int StatusCode { get; init; } = 200;

    public string ContentType { get; init; } = "application/json; charset=utf-8";

    public string Body { get; init; } = string.Empty;
}
