using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minio;
using Minio.DataModel.Args;
using PermissionSystem.Application.Files;

namespace PermissionSystem.Infrastructure.HealthChecks;

public sealed class MinioStorageHealthCheck : IHealthCheck
{
    private readonly IMinioClient _client;
    private readonly FileStorageOptions _options;

    public MinioStorageHealthCheck(IMinioClient client, FileStorageOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketName = _options.Minio.BucketName.Trim();
            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(bucketName),
                cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["provider"] = "Minio",
                ["endpoint"] = _options.Minio.Endpoint,
                ["bucket"] = bucketName
            };

            return exists
                ? HealthCheckResult.Healthy("MinIO storage is available.", data)
                : HealthCheckResult.Unhealthy("MinIO bucket does not exist.", data: data);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MinIO storage is unavailable.", exception);
        }
    }
}
