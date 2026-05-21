using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Files;

public sealed class MinioFileStorageService : IFileStorageService
{
    public string StorageProvider => "Minio";

    public Task<FileStorageSaveResult> SaveAsync(
        FileStorageSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("MinIO storage is reserved but not enabled in this build.");
    }

    public Task<Stream> OpenReadAsync(FileResource fileResource, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("MinIO storage is reserved but not enabled in this build.");
    }

    public Task DeleteAsync(FileResource fileResource, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("MinIO storage is reserved but not enabled in this build.");
    }
}
