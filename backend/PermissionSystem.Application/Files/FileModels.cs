using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.Files;

public sealed class FileResourceQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? BusinessType { get; init; }

    public Guid? BusinessId { get; init; }

    public string? StorageProvider { get; init; }

    public string? Extension { get; init; }
}

public sealed class UploadFileRequest
{
    public Guid? TenantId { get; init; }

    public Stream Content { get; init; } = Stream.Null;

    public string OriginalName { get; init; } = string.Empty;

    public string? ContentType { get; init; }

    public long Size { get; init; }

    public string? BusinessType { get; init; }

    public Guid? BusinessId { get; init; }
}

public sealed class FileResourceResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string OriginalName { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long Size { get; init; }

    public string StorageProvider { get; init; } = string.Empty;

    public string BucketName { get; init; } = string.Empty;

    public string ObjectKey { get; init; } = string.Empty;

    public string? Url { get; init; }

    public string Md5 { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public string? BusinessType { get; init; }

    public Guid? BusinessId { get; init; }

    public Guid? CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public FileStatus FileStatus { get; init; }

    public FileScanStatus ScanStatus { get; init; }

    public string? ScanMessage { get; init; }

    public DateTimeOffset? DeletedAt { get; init; }

    public DateTimeOffset? NextRetryAt { get; init; }

    public int RetryCount { get; init; }

    public string? LastError { get; init; }
}

public sealed class FileDownloadResponse
{
    public Stream Content { get; init; } = Stream.Null;

    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = "application/octet-stream";

    public long Size { get; init; }
}

public interface IFileService
{
    Task<PagedResult<FileResourceResponse>> GetPagedAsync(
        FileResourceQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileResourceResponse>> GetByBusinessAsync(
        string businessType,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<FileResourceResponse> UploadAsync(
        UploadFileRequest request,
        CancellationToken cancellationToken = default);

    Task<FileDownloadResponse> DownloadAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
