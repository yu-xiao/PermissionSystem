using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PermissionSystem.Infrastructure.Observability;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.UnitTests.Observability;

public sealed class LogArchiveServiceTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"permission-system-ea027-{Guid.NewGuid():N}");

    [Fact]
    public async Task ArchiveAsync_ShouldCompressExpiredActiveLogAndKeepArchiveWithinRetention()
    {
        var activeDirectory = Path.Combine(_rootDirectory, "active");
        var archiveDirectory = Path.Combine(_rootDirectory, "archive");
        Directory.CreateDirectory(activeDirectory);
        var sourcePath = Path.Combine(activeDirectory, "permission-system-api-20260801.log");
        await File.WriteAllTextAsync(sourcePath, "trace-ea027");
        File.SetLastWriteTimeUtc(sourcePath, DateTime.UtcNow.AddDays(-8));

        var service = CreateService(activeDirectory, archiveDirectory);

        await service.ArchiveAsync();

        var archivePath = Path.Combine(archiveDirectory, "permission-system-api-20260801.log.gz");
        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(archivePath));
        await using var stream = File.OpenRead(archivePath);
        await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        Assert.Equal("trace-ea027", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ArchiveAsync_ShouldDeleteExpiredCompressedArchive()
    {
        var activeDirectory = Path.Combine(_rootDirectory, "active");
        var archiveDirectory = Path.Combine(_rootDirectory, "archive");
        Directory.CreateDirectory(archiveDirectory);
        var expiredArchivePath = Path.Combine(archiveDirectory, "permission-system-api-20260601.log.gz");
        await File.WriteAllTextAsync(expiredArchivePath, "expired");
        File.SetLastWriteTimeUtc(expiredArchivePath, DateTime.UtcNow.AddDays(-46));

        var service = CreateService(activeDirectory, archiveDirectory);

        await service.ArchiveAsync();

        Assert.False(File.Exists(expiredArchivePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static LogArchiveService CreateService(string activeDirectory, string archiveDirectory)
    {
        return new LogArchiveService(
            Options.Create(new LogArchiveOptions
            {
                ActiveLogDirectory = activeDirectory,
                ArchiveDirectory = archiveDirectory,
                ActiveRetentionDays = 7,
                ArchiveRetentionDays = 45,
                CleanupIntervalMinutes = 60
            }),
            NullLogger<LogArchiveService>.Instance);
    }
}
