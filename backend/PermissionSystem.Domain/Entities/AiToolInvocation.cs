using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiToolInvocation : BaseEntity
{
    public Guid RunId { get; set; }

    public string InvocationId { get; set; } = string.Empty;

    public string ToolCode { get; set; } = string.Empty;

    public string ToolVersion { get; set; } = string.Empty;

    public AiInvocationStatus Status { get; set; } = AiInvocationStatus.Pending;

    public string InputDigest { get; set; } = string.Empty;

    public string? OutputDigest { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string? DatasetCode { get; set; }

    public string? DatasetVersion { get; set; }

    public int? RowCount { get; set; }

    public bool IsTruncated { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long? DurationMilliseconds { get; set; }

    public int RetryCount { get; set; }

    public string? ErrorCode { get; set; }

    public string? CitationJson { get; set; }
}
