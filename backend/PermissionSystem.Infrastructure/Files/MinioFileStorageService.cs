using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Files;
using PermissionSystem.Domain.Entities;
using Minio;
using Minio.DataModel.Args;

namespace PermissionSystem.Infrastructure.Files;

public sealed class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _client;
    private readonly MinioFileStorageOptions _options;

    public MinioFileStorageService(IMinioClient client, FileStorageOptions options)
    {
        _client = client;
        _options = options.Minio;
    }

    public string StorageProvider => "Minio";

    public FileStorageReference CreateReference(Guid fileId, string extension)
    {
        var bucketName = NormalizeBucketName(_options.BucketName);
        var normalizedExtension = NormalizeExtension(extension);
        return new FileStorageReference
        {
            StorageProvider = StorageProvider,
            BucketName = bucketName,
            ObjectKey = $"files/{fileId:N}{normalizedExtension}"
        };
    }

    public async Task<FileStorageSaveResult> SaveAsync(
        FileStorageSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var bucketName = NormalizeBucketName(request.Reference.BucketName);
        var objectKey = request.Reference.ObjectKey;

        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        await _client.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithStreamData(request.Content)
                .WithObjectSize(request.Size)
                .WithContentType(request.ContentType),
            cancellationToken);

        return new FileStorageSaveResult
        {
            StorageProvider = StorageProvider,
            BucketName = bucketName,
            ObjectKey = objectKey,
            Url = null
        };
    }

    public async Task<Stream> OpenReadAsync(FileResource fileResource, CancellationToken cancellationToken = default)
    {
        var content = new MemoryStream();
        await _client.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(NormalizeBucketName(fileResource.BucketName))
                .WithObject(fileResource.ObjectKey)
                .WithCallbackStream(stream => stream.CopyToAsync(content, cancellationToken)),
            cancellationToken);

        content.Position = 0;
        return content;
    }

    public async Task DeleteAsync(FileResource fileResource, CancellationToken cancellationToken = default)
    {
        await _client.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(NormalizeBucketName(fileResource.BucketName))
                .WithObject(fileResource.ObjectKey),
            cancellationToken);
    }

    private static string NormalizeBucketName(string? bucketName)
    {
        return string.IsNullOrWhiteSpace(bucketName) ? "permission-system" : bucketName.Trim();
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim().ToLowerInvariant();
        return normalized.StartsWith('.') ? normalized : $".{normalized}";
    }
}
