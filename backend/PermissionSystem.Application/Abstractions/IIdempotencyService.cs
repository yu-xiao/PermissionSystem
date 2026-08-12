namespace PermissionSystem.Application.Abstractions;

public interface IIdempotencyService
{
    Task<IdempotencyCacheEntry?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> TryBeginAsync(
        string key,
        IdempotencyCacheEntry entry,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    Task<bool> StoreAsync(
        string key,
        string operationId,
        IdempotencyCacheEntry entry,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, string? operationId = null, CancellationToken cancellationToken = default);

    Task<bool> TryAcquireDuplicateSubmitLockAsync(string key, TimeSpan expiresIn, CancellationToken cancellationToken = default);
}

public sealed class IdempotencyCacheEntry
{
    public string State { get; init; } = "Completed";

    public string OperationId { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string RequestBodyHash { get; init; } = string.Empty;

    public int StatusCode { get; init; } = 200;

    public string ContentType { get; init; } = "application/json; charset=utf-8";

    public string Body { get; init; } = string.Empty;

    public string ResponseBodyHash { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }
}
