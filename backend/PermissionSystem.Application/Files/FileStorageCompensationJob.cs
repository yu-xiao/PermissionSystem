using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace PermissionSystem.Application.Files;

public sealed class FileStorageCompensationJob
{
    private readonly IRepository<FileResource> _fileRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFileContentScanner _fileContentScanner;
    private readonly ISystemTenantScope _systemTenantScope;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FileStorageCompensationJob> _logger;

    public FileStorageCompensationJob(
        IRepository<FileResource> fileRepository,
        IFileStorageService fileStorageService,
        IFileContentScanner fileContentScanner,
        ISystemTenantScope systemTenantScope,
        IUnitOfWork unitOfWork,
        ILogger<FileStorageCompensationJob> logger)
    {
        _fileRepository = fileRepository;
        _fileStorageService = fileStorageService;
        _fileContentScanner = fileContentScanner;
        _systemTenantScope = systemTenantScope;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        using var systemScope = _systemTenantScope.Begin(SystemTenantOperations.FileStorageCompensation);
        var now = DateTimeOffset.UtcNow;
        var files = _fileRepository.Query()
            .Where(entity =>
                (entity.FileStatus == FileStatus.Pending || entity.FileStatus == FileStatus.PendingDelete) &&
                (!entity.NextRetryAt.HasValue || entity.NextRetryAt <= now))
            .OrderBy(entity => entity.CreatedAt)
            .Take(100)
            .ToList();

        foreach (var file in files)
        {
            try
            {
                if (file.FileStatus == FileStatus.PendingDelete)
                {
                    await CompleteDeleteAsync(file);
                }
                else
                {
                    await CompletePendingUploadAsync(file);
                }
            }
            catch (Exception exception)
            {
                await RecordRetryAsync(file, exception);
                _logger.LogWarning(
                    exception,
                    "File storage compensation failed for file {FileId}.",
                    file.Id);
            }
        }
    }

    private async Task CompletePendingUploadAsync(FileResource file)
    {
        if (string.IsNullOrWhiteSpace(file.ObjectKey))
        {
            file.FileStatus = FileStatus.Failed;
            file.ScanStatus = FileScanStatus.Failed;
            file.LastError = "Pending file has no storage object key.";
            file.NextRetryAt = null;
            _fileRepository.Update(file);
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        await using var content = await _fileStorageService.OpenReadAsync(file);
        var scanResult = await _fileContentScanner.ScanAsync(
            new FileScanRequest
            {
                Content = content,
                FileName = file.OriginalName,
                Extension = file.Extension,
                ClientContentType = file.ContentType
            });

        if (!scanResult.IsClean)
        {
            file.ScanStatus = FileScanStatus.Infected;
            file.ScanMessage = scanResult.Message;
            file.FileStatus = FileStatus.PendingDelete;
            file.LastError = scanResult.Message;
            file.NextRetryAt = null;
            _fileRepository.Update(file);
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        file.ContentType = scanResult.DetectedContentType;
        file.ScanStatus = FileScanStatus.Clean;
        file.FileStatus = FileStatus.Active;
        file.ScanMessage = null;
        file.LastError = null;
        file.NextRetryAt = null;
        _fileRepository.Update(file);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task CompleteDeleteAsync(FileResource file)
    {
        await _fileStorageService.DeleteAsync(file);
        file.FileStatus = FileStatus.Deleted;
        file.DeletedAt = DateTimeOffset.UtcNow;
        file.NextRetryAt = null;
        file.LastError = null;
        _fileRepository.Remove(file);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task RecordRetryAsync(FileResource file, Exception exception)
    {
        file.RetryCount++;
        file.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(
            Math.Min(60, Math.Pow(2, Math.Min(file.RetryCount, 6))));
        file.LastError = exception.Message.Length > 2000
            ? exception.Message[..2000]
            : exception.Message;
        _fileRepository.Update(file);
        await _unitOfWork.SaveChangesAsync();
    }
}
