using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Application.Abstractions;

public interface IFileStorageService
{
    string StorageProvider { get; }

    FileStorageReference CreateReference(
        Guid fileId,
        string extension);

    Task<FileStorageSaveResult> SaveAsync(
        FileStorageSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(FileResource fileResource, CancellationToken cancellationToken = default);

    Task DeleteAsync(FileResource fileResource, CancellationToken cancellationToken = default);
}

public sealed class FileStorageSaveRequest
{
    public Stream Content { get; init; } = Stream.Null;

    public FileStorageReference Reference { get; init; } = new();

    public string ContentType { get; init; } = "application/octet-stream";

    public long Size { get; init; }
}

public sealed class FileStorageReference
{
    public string StorageProvider { get; init; } = string.Empty;

    public string BucketName { get; init; } = string.Empty;

    public string ObjectKey { get; init; } = string.Empty;

    public string? Url { get; init; }
}

public sealed class FileStorageSaveResult
{
    public string StorageProvider { get; init; } = string.Empty;

    public string BucketName { get; init; } = string.Empty;

    public string ObjectKey { get; init; } = string.Empty;

    public string? Url { get; init; }
}
