namespace PermissionSystem.Application.Excels;

public sealed class ImportError
{
    public int RowNumber { get; init; }

    public string ColumnName { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? RawValue { get; init; }
}

public sealed class ImportResult<T>
{
    public int TotalRows { get; init; }

    public int SuccessRows { get; init; }

    public int FailedRows { get; init; }

    public IReadOnlyList<T> Items { get; init; } = [];

    public IReadOnlyList<ImportError> Errors { get; init; } = [];
}

public sealed class ExportRequest<T>
{
    public string SheetName { get; init; } = "Sheet1";

    public IReadOnlyCollection<T> Items { get; init; } = [];
}

public interface IExcelService
{
    Task<byte[]> ExportAsync<T>(ExportRequest<T> request, CancellationToken cancellationToken = default)
        where T : class;

    Task<ImportResult<T>> ImportAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        where T : class, new();

    Task<byte[]> CreateTemplateAsync<T>(string sheetName = "Template", CancellationToken cancellationToken = default)
        where T : class, new();
}
