using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class JobExecutionLog : BaseEntity
{
    public string JobName { get; set; } = string.Empty;

    public string? JobId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public string? ErrorMessage { get; set; }

    public string? TraceId { get; set; }
}
