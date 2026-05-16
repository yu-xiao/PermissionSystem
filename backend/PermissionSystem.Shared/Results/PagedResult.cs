namespace PermissionSystem.Shared.Results;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int PageIndex { get; init; }

    public int PageSize { get; init; }

    public long TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageIndex > 1;

    public bool HasNextPage => PageIndex < TotalPages;

    public static PagedResult<T> Create(IReadOnlyList<T> items, int pageIndex, int pageSize, long totalCount)
    {
        return new PagedResult<T>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
