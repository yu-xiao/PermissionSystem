using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class FileResource : BaseEntity
{
    public string OriginalName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    public string StorageProvider { get; set; } = "Local";

    public string BucketName { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string Md5 { get; set; } = string.Empty;

    public string? BusinessType { get; set; }

    public Guid? BusinessId { get; set; }
}
