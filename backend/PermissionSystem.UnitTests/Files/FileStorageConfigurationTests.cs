using Microsoft.Extensions.Diagnostics.HealthChecks;
using PermissionSystem.Application.Files;
using PermissionSystem.Infrastructure.Files;
using PermissionSystem.Infrastructure.HealthChecks;

namespace PermissionSystem.UnitTests.Files;

public sealed class FileStorageConfigurationTests
{
    [Fact]
    public void UnsupportedProvider_ShouldBeRejected()
    {
        var options = new FileStorageOptions { Provider = "S3" };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            FileStorageConfigurationValidator.Validate(options, "Development"));

        Assert.Contains("Local", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Minio", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MinioProvider_ShouldRequireConnectionConfiguration()
    {
        var options = new FileStorageOptions { Provider = "Minio" };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            FileStorageConfigurationValidator.Validate(options, "Development"));

        Assert.Contains("Endpoint", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionLocalProvider_ShouldRequireAbsoluteRootPath()
    {
        var options = new FileStorageOptions
        {
            Provider = "Local",
            Local = new LocalFileStorageOptions { RootPath = "uploads" }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            FileStorageConfigurationValidator.Validate(options, "Production"));

        Assert.Contains("absolute path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalStorageHealthCheck_ShouldProbeConfiguredDirectory()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"permission-system-files-{Guid.NewGuid():N}");
        try
        {
            var check = new DiskStorageHealthCheck(new FileStorageOptions
            {
                Provider = "Local",
                Local = new LocalFileStorageOptions { RootPath = rootPath }
            });

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal(rootPath, result.Data["path"]);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
