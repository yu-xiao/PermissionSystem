using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class ScheduledTask : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string JobType { get; set; } = string.Empty;

    public string CronExpression { get; set; } = string.Empty;

    public string Queue { get; set; } = "default";

    public string? Description { get; set; }

    public string? ParametersJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? LastRunAt { get; set; }

    public bool? LastRunSucceeded { get; set; }

    public string? LastRunMessage { get; set; }

    public string? LastJobId { get; set; }

    public ICollection<ScheduledTaskExecutionLog> ExecutionLogs { get; set; } = new List<ScheduledTaskExecutionLog>();
}
