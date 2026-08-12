using System.Security.Cryptography;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Files;

public sealed class FileService : IFileService
{
    private static readonly StringComparer ExtensionComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IRepository<FileResource> _fileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFileContentScanner _fileContentScanner;
    private readonly IFileBusinessAccessChecker _fileBusinessAccessChecker;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly FileStorageOptions _options;

    public FileService(
        IRepository<FileResource> fileRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IFileContentScanner fileContentScanner,
        IFileBusinessAccessChecker fileBusinessAccessChecker,
        ICurrentUserService currentUserService,
        ITenantWriteResolver tenantWriteResolver,
        FileStorageOptions options)
    {
        _fileRepository = fileRepository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _fileContentScanner = fileContentScanner;
        _fileBusinessAccessChecker = fileBusinessAccessChecker;
        _currentUserService = currentUserService;
        _tenantWriteResolver = tenantWriteResolver;
        _options = options;
    }

    public async Task<PagedResult<FileResourceResponse>> GetPagedAsync(
        FileResourceQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _fileRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.OriginalName.Contains(keyword) ||
                entity.FileName.Contains(keyword) ||
                entity.Md5.Contains(keyword) ||
                (entity.BusinessType != null && entity.BusinessType.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessType))
        {
            var businessType = request.BusinessType.Trim();
            query = query.Where(entity => entity.BusinessType == businessType);
        }

        if (request.BusinessId.HasValue)
        {
            query = query.Where(entity => entity.BusinessId == request.BusinessId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.StorageProvider))
        {
            var storageProvider = request.StorageProvider.Trim();
            query = query.Where(entity => entity.StorageProvider == storageProvider);
        }

        if (!string.IsNullOrWhiteSpace(request.Extension))
        {
            var extension = NormalizeExtension(request.Extension);
            query = query.Where(entity => entity.Extension == extension);
        }

        var accessibleItems = new List<FileResource>();
        foreach (var fileResource in query.ToList())
        {
            if (await _fileBusinessAccessChecker.CanAccessAsync(
                    fileResource.BusinessType,
                    fileResource.BusinessId,
                    cancellationToken))
            {
                accessibleItems.Add(fileResource);
            }
        }

        var totalCount = accessibleItems.Count;
        var items = accessibleItems
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToResponse)
            .ToList();

        return PagedResult<FileResourceResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount);
    }

    public async Task<IReadOnlyList<FileResourceResponse>> GetByBusinessAsync(
        string businessType,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(businessType, "Business type is required.");
        if (!await _fileBusinessAccessChecker.CanAccessAsync(businessType, businessId, cancellationToken))
        {
            throw new BusinessException(ErrorCode.NotFound, "Business object was not found.");
        }

        var normalizedBusinessType = businessType.Trim();
        var items = _fileRepository.Query()
            .Where(entity =>
                entity.BusinessType == normalizedBusinessType &&
                entity.BusinessId == businessId &&
                entity.FileStatus == FileStatus.Active &&
                entity.ScanStatus == FileScanStatus.Clean)
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(ToResponse)
            .ToList();

        return items;
    }

    public async Task<FileResourceResponse> UploadAsync(
        UploadFileRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpload(request);
        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        if (request.BusinessType is not null &&
            !await _fileBusinessAccessChecker.CanAccessAsync(
                request.BusinessType,
                request.BusinessId,
                cancellationToken))
        {
            throw new BusinessException(ErrorCode.NotFound, "Business object was not found.");
        }

        var originalName = NormalizeOriginalName(request.OriginalName);
        var extension = NormalizeExtension(Path.GetExtension(originalName));
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"permission-system-file-{Guid.NewGuid():N}.tmp");

        try
        {
            var hashes = await CopyToTemporaryFileAsync(request, temporaryPath, cancellationToken);
            await using (var scanStream = new FileStream(
                temporaryPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var scanResult = await _fileContentScanner.ScanAsync(
                    new FileScanRequest
                    {
                        Content = scanStream,
                        FileName = originalName,
                        Extension = extension,
                        ClientContentType = request.ContentType
                    },
                    cancellationToken);

                if (!scanResult.IsClean)
                {
                    ObservabilityMetrics.RecordFileScanFailure();
                    throw new BusinessException(
                        ErrorCode.ValidationFailed,
                        scanResult.Message ?? "File security scan failed.");
                }

                var fileId = Guid.NewGuid();
                var storageReference = _fileStorageService.CreateReference(fileId, extension);
                var fileResource = new FileResource
                {
                    Id = fileId,
                    TenantId = tenantId,
                    OriginalName = originalName,
                    FileName = fileName,
                    Extension = extension,
                    ContentType = scanResult.DetectedContentType,
                    Size = request.Size,
                    StorageProvider = storageReference.StorageProvider,
                    BucketName = storageReference.BucketName,
                    ObjectKey = storageReference.ObjectKey,
                    Url = null,
                    Md5 = hashes.Md5,
                    Sha256 = hashes.Sha256,
                    BusinessType = NormalizeOptional(request.BusinessType),
                    BusinessId = request.BusinessId,
                    FileStatus = FileStatus.Pending,
                    ScanStatus = FileScanStatus.Clean,
                    CreatedBy = _currentUserService.UserId
                };

                await _fileRepository.AddAsync(fileResource, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                try
                {
                    await using var content = new FileStream(
                        temporaryPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        81920,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await _fileStorageService.SaveAsync(
                        new FileStorageSaveRequest
                        {
                            Content = content,
                            Reference = storageReference,
                            ContentType = scanResult.DetectedContentType,
                            Size = request.Size
                        },
                        cancellationToken);

                    fileResource.FileStatus = FileStatus.Active;
                    fileResource.NextRetryAt = null;
                    fileResource.LastError = null;
                    _fileRepository.Update(fileResource);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    fileResource.LastError = TruncateError(exception.Message);
                    fileResource.RetryCount++;
                    fileResource.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(5);
                    _fileRepository.Update(fileResource);
                    try
                    {
                        await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                    }
                    catch
                    {
                        // The durable Pending row is enough for the compensation job.
                    }

                    throw;
                }

                return ToResponse(fileResource);
            }
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    public async Task<FileDownloadResponse> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fileResource = await GetFileOrThrowAsync(id, cancellationToken);
        await _fileBusinessAccessChecker.EnsureAccessAsync(fileResource, cancellationToken);
        var stream = await _fileStorageService.OpenReadAsync(fileResource, cancellationToken);

        return new FileDownloadResponse
        {
            Content = stream,
            FileName = fileResource.OriginalName,
            ContentType = fileResource.ContentType,
            Size = fileResource.Size
        };
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fileResource = await GetFileOrThrowAsync(id, cancellationToken);
        await _fileBusinessAccessChecker.EnsureAccessAsync(fileResource, cancellationToken);

        fileResource.FileStatus = FileStatus.PendingDelete;
        fileResource.NextRetryAt = null;
        fileResource.LastError = null;
        _fileRepository.Update(fileResource);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<FileResource> GetFileOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        var fileResource = await _fileRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "File was not found.");
        if (fileResource.FileStatus != FileStatus.Active ||
            fileResource.ScanStatus != FileScanStatus.Clean)
        {
            throw new BusinessException(ErrorCode.Conflict, "File is not available.");
        }

        return fileResource;
    }

    private void ValidateUpload(UploadFileRequest request)
    {
        if (request.Content == Stream.Null || !request.Content.CanRead)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "File content is required.");
        }

        if (request.Size <= 0)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "File is empty.");
        }

        if (request.Size > _options.MaxFileSizeBytes)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "File is too large.");
        }

        var originalName = NormalizeOriginalName(request.OriginalName);
        var extension = NormalizeExtension(Path.GetExtension(originalName));

        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "File extension is required.");
        }

        if (_options.BlockedExtensions.Contains(extension, ExtensionComparer))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Executable files are not allowed.");
        }

        if (_options.AllowedExtensions.Length > 0 &&
            !_options.AllowedExtensions.Select(NormalizeExtension).Contains(extension, ExtensionComparer))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "File extension is not allowed.");
        }

        var contentType = NormalizeContentType(request.ContentType);
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (_options.BlockedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "File content type is not allowed.");
            }

            if (_options.AllowedContentTypes.Length > 0 &&
                !_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "File content type is not allowed.");
            }
        }
    }

    private static FileResourceResponse ToResponse(FileResource fileResource)
    {
        return new FileResourceResponse
        {
            Id = fileResource.Id,
            TenantId = fileResource.TenantId,
            OriginalName = fileResource.OriginalName,
            FileName = fileResource.FileName,
            Extension = fileResource.Extension,
            ContentType = fileResource.ContentType,
            Size = fileResource.Size,
            StorageProvider = fileResource.StorageProvider,
            BucketName = fileResource.BucketName,
            ObjectKey = fileResource.ObjectKey,
            Url = null,
            Md5 = fileResource.Md5,
            Sha256 = fileResource.Sha256,
            BusinessType = fileResource.BusinessType,
            BusinessId = fileResource.BusinessId,
            CreatedBy = fileResource.CreatedBy,
            CreatedAt = fileResource.CreatedAt,
            FileStatus = fileResource.FileStatus,
            ScanStatus = fileResource.ScanStatus,
            ScanMessage = fileResource.ScanMessage,
            DeletedAt = fileResource.DeletedAt,
            NextRetryAt = fileResource.NextRetryAt,
            RetryCount = fileResource.RetryCount,
            LastError = fileResource.LastError
        };
    }

    private static async Task<FileHashResult> CopyToTemporaryFileAsync(
        UploadFileRequest request,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var temporaryStream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await request.Content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > request.Size || total > 1024L * 1024L * 1024L)
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Uploaded file size is invalid.");
            }

            await temporaryStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            md5.AppendData(buffer, 0, read);
            sha256.AppendData(buffer, 0, read);
        }

        if (total != request.Size)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Uploaded file size is invalid.");
        }

        return new FileHashResult(
            Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant(),
            Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant());
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // The temporary file is outside application storage and can be removed by the OS.
        }
    }

    private static string TruncateError(string message)
    {
        return message.Length > 2000 ? message[..2000] : message;
    }

    private readonly record struct FileHashResult(string Md5, string Sha256);

    private static string NormalizeOriginalName(string originalName)
    {
        ValidateRequired(originalName, "Original file name is required.");

        var trimmed = originalName.Trim();
        var fileName = Path.GetFileName(trimmed);
        if (fileName != trimmed ||
            fileName.Contains("..", StringComparison.Ordinal) ||
            fileName.Any(char.IsControl))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid file name.");
        }

        return fileName;
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

    private static string? NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        return contentType.Split(';')[0].Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }
}
