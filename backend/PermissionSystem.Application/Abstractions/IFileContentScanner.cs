namespace PermissionSystem.Application.Abstractions;

public interface IFileContentScanner
{
    Task<FileScanResult> ScanAsync(
        FileScanRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class FileScanRequest
{
    public Stream Content { get; init; } = Stream.Null;

    public string FileName { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public string? ClientContentType { get; init; }
}

public sealed class FileScanResult
{
    public bool IsClean { get; init; }

    public string DetectedContentType { get; init; } = "application/octet-stream";

    public string? Message { get; init; }
}
