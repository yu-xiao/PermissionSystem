using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.Infrastructure.Observability;

public sealed class LogArchiveService
{
    private readonly LogArchiveOptions _options;
    private readonly ILogger<LogArchiveService> _logger;

    public LogArchiveService(IOptions<LogArchiveOptions> options, ILogger<LogArchiveService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task ArchiveAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        ValidateOptions();
        return ArchiveCoreAsync(cancellationToken);
    }

    private async Task ArchiveCoreAsync(CancellationToken cancellationToken)
    {
        var activeDirectory = Path.GetFullPath(_options.ActiveLogDirectory);
        var archiveDirectory = Path.GetFullPath(_options.ArchiveDirectory);
        Directory.CreateDirectory(activeDirectory);
        Directory.CreateDirectory(archiveDirectory);

        var archiveBefore = DateTime.UtcNow.AddDays(-_options.ActiveRetentionDays);
        foreach (var sourcePath in Directory.EnumerateFiles(activeDirectory, "*.log", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.GetLastWriteTimeUtc(sourcePath) >= archiveBefore)
            {
                continue;
            }

            await CompressAndMoveAsync(sourcePath, archiveDirectory, cancellationToken);
        }

        var deleteBefore = DateTime.UtcNow.AddDays(-_options.ArchiveRetentionDays);
        foreach (var archivePath in Directory.EnumerateFiles(archiveDirectory, "*.gz", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.GetLastWriteTimeUtc(archivePath) < deleteBefore)
            {
                File.Delete(archivePath);
                _logger.LogInformation("Expired log archive deleted. Path: {Path}", archivePath);
            }
        }
    }

    private async Task CompressAndMoveAsync(string sourcePath, string archiveDirectory, CancellationToken cancellationToken)
    {
        var archivePath = Path.Combine(archiveDirectory, $"{Path.GetFileName(sourcePath)}.gz");
        if (File.Exists(archivePath))
        {
            _logger.LogWarning("Log archive already exists; source log is retained. Source: {SourcePath}, Archive: {ArchivePath}", sourcePath, archivePath);
            return;
        }

        var temporaryArchivePath = $"{archivePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = new FileStream(
                temporaryArchivePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var gzip = new GZipStream(destination, CompressionLevel.SmallestSize, leaveOpen: false))
            {
                await source.CopyToAsync(gzip, cancellationToken);
            }

            File.Move(temporaryArchivePath, archivePath);
            File.Delete(sourcePath);
            _logger.LogInformation("Log file archived. Source: {SourcePath}, Archive: {ArchivePath}", sourcePath, archivePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Log archive attempt failed. Source: {SourcePath}", sourcePath);
        }
        finally
        {
            if (File.Exists(temporaryArchivePath))
            {
                File.Delete(temporaryArchivePath);
            }
        }
    }

    private void ValidateOptions()
    {
        if (_options.ActiveRetentionDays < 1 || _options.ArchiveRetentionDays < 1 || _options.CleanupIntervalMinutes < 1)
        {
            throw new InvalidOperationException("LogArchive retention and cleanup intervals must be greater than zero.");
        }

        var activeDirectory = Path.GetFullPath(_options.ActiveLogDirectory);
        var archiveDirectory = Path.GetFullPath(_options.ArchiveDirectory);
        if (string.Equals(activeDirectory, archiveDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("LogArchive active and archive directories must be different.");
        }
    }
}
