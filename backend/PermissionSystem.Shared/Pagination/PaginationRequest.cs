namespace PermissionSystem.Shared.Pagination;

public class PaginationRequest
{
    private const int MaxPageSize = 200;
    private int _pageIndex = 1;
    private int _pageSize = 20;

    public int PageIndex
    {
        get => _pageIndex;
        init => _pageIndex = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public string? SortBy { get; init; }

    public bool Descending { get; init; }

    public int Skip => (PageIndex - 1) * PageSize;
}
