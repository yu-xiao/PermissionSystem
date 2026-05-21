namespace PermissionSystem.Application.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string Provider { get; init; } = "Local";

    public long MaxFileSizeBytes { get; init; } = 20 * 1024 * 1024;

    public string[] AllowedExtensions { get; init; } =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp",
        ".pdf",
        ".txt",
        ".csv",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".ppt",
        ".pptx",
        ".zip",
        ".rar"
    ];

    public string[] BlockedExtensions { get; init; } =
    [
        ".exe",
        ".dll",
        ".bat",
        ".cmd",
        ".com",
        ".scr",
        ".msi",
        ".ps1",
        ".sh",
        ".js",
        ".vbs",
        ".jar",
        ".apk"
    ];

    public string[] AllowedContentTypes { get; init; } = [];

    public string[] BlockedContentTypes { get; init; } =
    [
        "application/x-msdownload",
        "application/x-msdos-program",
        "application/x-msi",
        "application/x-sh",
        "application/x-powershell",
        "application/javascript",
        "text/javascript",
        "application/java-archive",
        "application/vnd.android.package-archive"
    ];

    public LocalFileStorageOptions Local { get; init; } = new();

    public MinioFileStorageOptions Minio { get; init; } = new();
}

public sealed class LocalFileStorageOptions
{
    public string RootPath { get; init; } = "uploads";

    public string BucketName { get; init; } = "default";

    public string? PublicBaseUrl { get; init; }
}

public sealed class MinioFileStorageOptions
{
    public string Endpoint { get; init; } = string.Empty;

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public string BucketName { get; init; } = "permission-system";

    public bool UseSsl { get; init; }

    public string? PublicBaseUrl { get; init; }
}
