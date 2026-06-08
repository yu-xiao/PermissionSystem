using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class ExternalApiCallLog : BaseEntity
{
    public Guid? ClientId { get; set; }

    public string Path { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public int StatusCode { get; set; }

    public long ElapsedMilliseconds { get; set; }
}
