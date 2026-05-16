using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class OperationLog : BaseEntity
{
    public Guid? OperatorUserId { get; set; }

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string? RequestPath { get; set; }

    public string? HttpMethod { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public bool Succeeded { get; set; }

    public string? Message { get; set; }

    public DateTimeOffset OperatedAt { get; set; }
}
