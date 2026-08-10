using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Infrastructure.Queries;

public sealed class EfCoreAsyncQueryExecutor : IAsyncQueryExecutor
{
    public async Task<IReadOnlyList<T>> ToListAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        return await query.ToListAsync(cancellationToken);
    }

    public Task<long> LongCountAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        return query.LongCountAsync(cancellationToken);
    }

    public Task<bool> AnyAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        return query.AnyAsync(cancellationToken);
    }

    public Task<T?> FirstOrDefaultAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        return query.FirstOrDefaultAsync(cancellationToken);
    }
}
