using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Files;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Files;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;

    public LocalFileStorageService(FileStorageOptions options)
    {
        _options = options;
    }

    public string StorageProvider => "Local";

    public FileStorageReference CreateReference(Guid fileId, string extension)
    {
        var bucketName = NormalizeBucketName(_options.Local.BucketName);
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
        var rootPath = GetRootPath();
        var fullPath = ResolveSafePath(rootPath, bucketName, objectKey);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        request.Content.Position = 0;
        await using (var fileStream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await request.Content.CopyToAsync(fileStream, cancellationToken);
        }

        return new FileStorageSaveResult
        {
            StorageProvider = StorageProvider,
            BucketName = bucketName,
            ObjectKey = objectKey,
            Url = null
        };
    }

    public Task<Stream> OpenReadAsync(FileResource fileResource, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(GetRootPath(), fileResource.BucketName, fileResource.ObjectKey);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Stored file was not found.", fileResource.ObjectKey);
        }

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(FileResource fileResource, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(GetRootPath(), fileResource.BucketName, fileResource.ObjectKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string GetRootPath()
    {
        var configuredRoot = string.IsNullOrWhiteSpace(_options.Local.RootPath)
            ? "uploads"
            : _options.Local.RootPath;

        return Path.GetFullPath(configuredRoot);
    }

    private string ResolveSafePath(string rootPath, string bucketName, string objectKey)
    {
        var normalizedObjectKey = objectKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, bucketName, normalizedObjectKey));
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid local storage path.");
        }

        return fullPath;
    }

    private static string NormalizeBucketName(string? bucketName)
    {
        return string.IsNullOrWhiteSpace(bucketName) ? "default" : bucketName.Trim();
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
