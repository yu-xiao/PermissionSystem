using System.Security.Cryptography;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
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
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly FileStorageOptions _options;

    public FileService(
        IRepository<FileResource> fileRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        ITenantWriteResolver tenantWriteResolver,
        FileStorageOptions options)
    {
        _fileRepository = fileRepository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _tenantWriteResolver = tenantWriteResolver;
        _options = options;
    }

    public Task<PagedResult<FileResourceResponse>> GetPagedAsync(
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

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<FileResourceResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public Task<IReadOnlyList<FileResourceResponse>> GetByBusinessAsync(
        string businessType,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(businessType, "Business type is required.");

        var normalizedBusinessType = businessType.Trim();
        var items = _fileRepository.Query()
            .Where(entity => entity.BusinessType == normalizedBusinessType && entity.BusinessId == businessId)
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult<IReadOnlyList<FileResourceResponse>>(items);
    }

    public async Task<FileResourceResponse> UploadAsync(
        UploadFileRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpload(request);
        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);

        var originalName = NormalizeOriginalName(request.OriginalName);
        var extension = NormalizeExtension(Path.GetExtension(originalName));
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType.Trim();

        await using var memoryStream = new MemoryStream();
        await request.Content.CopyToAsync(memoryStream, cancellationToken);
        if (memoryStream.Length != request.Size)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Uploaded file size is invalid.");
        }

        var md5 = Convert.ToHexString(MD5.HashData(memoryStream.ToArray())).ToLowerInvariant();
        memoryStream.Position = 0;

        var storageResult = await _fileStorageService.SaveAsync(
            new FileStorageSaveRequest
            {
                Content = memoryStream,
                OriginalName = originalName,
                FileName = fileName,
                Extension = extension,
                ContentType = contentType,
                Size = request.Size
            },
            cancellationToken);

        var fileResource = new FileResource
        {
            TenantId = tenantId,
            OriginalName = originalName,
            FileName = fileName,
            Extension = extension,
            ContentType = contentType,
            Size = request.Size,
            StorageProvider = storageResult.StorageProvider,
            BucketName = storageResult.BucketName,
            ObjectKey = storageResult.ObjectKey,
            Url = storageResult.Url,
            Md5 = md5,
            BusinessType = NormalizeOptional(request.BusinessType),
            BusinessId = request.BusinessId,
            CreatedBy = _currentUserService.UserId
        };

        await _fileRepository.AddAsync(fileResource, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(fileResource);
    }

    public async Task<FileDownloadResponse> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fileResource = await GetFileOrThrowAsync(id, cancellationToken);
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
        await _fileStorageService.DeleteAsync(fileResource, cancellationToken);

        _fileRepository.Remove(fileResource);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<FileResource> GetFileOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _fileRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "File was not found.");
    }

    private void ValidateUpload(UploadFileRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Tenant is required.");
        }

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
            Url = fileResource.Url,
            Md5 = fileResource.Md5,
            BusinessType = fileResource.BusinessType,
            BusinessId = fileResource.BusinessId,
            CreatedBy = fileResource.CreatedBy,
            CreatedAt = fileResource.CreatedAt
        };
    }

    private static string NormalizeOriginalName(string originalName)
    {
        ValidateRequired(originalName, "Original file name is required.");

        var trimmed = originalName.Trim();
        var fileName = Path.GetFileName(trimmed);
        if (fileName != trimmed || fileName.Contains("..", StringComparison.Ordinal))
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
