using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class McpInvocationLog : BaseEntity
{
    public Guid? ClientBindingId { get; set; }

    public McpCallerType CallerType { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? OAuthClientId { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public string? DatasetCode { get; set; }

    public string TraceId { get; set; } = string.Empty;

    public string InputDigest { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public McpInvocationStatus Status { get; set; }

    public int RowCount { get; set; }

    public bool IsTruncated { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public long DurationMilliseconds { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorSummary { get; set; }
}
