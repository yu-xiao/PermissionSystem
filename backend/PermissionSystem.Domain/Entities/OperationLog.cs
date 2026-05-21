using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class OperationLog : BaseEntity
{
    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string? RequestPath { get; set; }

    public string RequestMethod { get; set; } = string.Empty;

    public string? RequestBody { get; set; }

    public string? ResponseBody { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public int StatusCode { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public string? TraceId { get; set; }
}
