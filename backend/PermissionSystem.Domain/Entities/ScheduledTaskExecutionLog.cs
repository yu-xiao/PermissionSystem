using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class ScheduledTaskExecutionLog : BaseEntity
{
    public Guid ScheduledTaskId { get; set; }

    public ScheduledTask? ScheduledTask { get; set; }

    public string? JobId { get; set; }

    public string JobType { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public bool Succeeded { get; set; }

    public string? TraceId { get; set; }

    public string? Message { get; set; }

    public string? ParametersJson { get; set; }
}
