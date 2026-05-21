using Microsoft.Extensions.Diagnostics.HealthChecks;
using PermissionSystem.Application.Files;

namespace PermissionSystem.Infrastructure.HealthChecks;

public sealed class DiskStorageHealthCheck : IHealthCheck
{
    private readonly FileStorageOptions _options;

    public DiskStorageHealthCheck(FileStorageOptions options)
    {
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(_options.Provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return HealthCheckResult.Healthy(
                $"Disk storage check skipped for provider '{_options.Provider}'.",
                new Dictionary<string, object>
                {
                    ["provider"] = _options.Provider
                });
        }

        try
        {
            var rootPath = Path.GetFullPath(
                string.IsNullOrWhiteSpace(_options.Local.RootPath)
                    ? "uploads"
                    : _options.Local.RootPath);

            Directory.CreateDirectory(rootPath);

            var probeFile = Path.Combine(rootPath, $".health-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(probeFile, "ok", cancellationToken);
            File.Delete(probeFile);

            var data = new Dictionary<string, object>
            {
                ["provider"] = _options.Provider,
                ["path"] = rootPath
            };

            var root = Path.GetPathRoot(rootPath);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var driveInfo = new DriveInfo(root);
                data["availableFreeSpaceBytes"] = driveInfo.AvailableFreeSpace;
                data["totalSizeBytes"] = driveInfo.TotalSize;
            }

            return HealthCheckResult.Healthy("Disk storage is writable.", data);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Disk storage is unavailable.", exception);
        }
    }
}
