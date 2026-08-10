using PermissionSystem.Application.Files;

namespace PermissionSystem.Infrastructure.Files;

public static class FileStorageConfigurationValidator
{
    public static void Validate(FileStorageOptions options, string? environmentName)
    {
        var provider = options.Provider?.Trim();
        if (!string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(provider, "Minio", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"FileStorage:Provider '{options.Provider}' is not supported. Use 'Local' or 'Minio'.");
        }

        if (string.Equals(provider, "Minio", StringComparison.OrdinalIgnoreCase))
        {
            var minio = options.Minio;
            if (string.IsNullOrWhiteSpace(minio.Endpoint) ||
                string.IsNullOrWhiteSpace(minio.AccessKey) ||
                string.IsNullOrWhiteSpace(minio.SecretKey) ||
                string.IsNullOrWhiteSpace(minio.BucketName))
            {
                throw new InvalidOperationException(
                    "FileStorage:Minio requires Endpoint, AccessKey, SecretKey, and BucketName when Minio is selected.");
            }

            return;
        }

        if (string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(options.Local.RootPath) || !Path.IsPathRooted(options.Local.RootPath)))
        {
            throw new InvalidOperationException(
                "FileStorage:Local:RootPath must be an absolute path in Production so it can be backed by a persistent volume.");
        }
    }
}
