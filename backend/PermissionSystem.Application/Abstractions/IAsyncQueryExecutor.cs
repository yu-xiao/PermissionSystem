namespace PermissionSystem.Application.Abstractions;

public interface IAsyncQueryExecutor
{
    Task<IReadOnlyList<T>> ToListAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default);

    Task<long> LongCountAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default);

    Task<bool> AnyAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default);
}
