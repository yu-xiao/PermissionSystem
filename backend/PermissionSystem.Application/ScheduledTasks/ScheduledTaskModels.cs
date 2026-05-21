using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.ScheduledTasks;

public static class ScheduledTaskJobTypes
{
    public const string DemoLog = "DemoLog";
}

public sealed class ScheduledTaskQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? JobType { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class ScheduledTaskLogQueryRequest : PaginationRequest
{
}

public sealed class CreateScheduledTaskRequest
{
    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string JobType { get; init; } = ScheduledTaskJobTypes.DemoLog;

    public string CronExpression { get; init; } = string.Empty;

    public string Queue { get; init; } = "default";

    public string? Description { get; init; }

    public string? ParametersJson { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public sealed class UpdateScheduledTaskRequest
{
    public string Name { get; init; } = string.Empty;

    public string JobType { get; init; } = ScheduledTaskJobTypes.DemoLog;

    public string CronExpression { get; init; } = string.Empty;

    public string Queue { get; init; } = "default";

    public string? Description { get; init; }

    public string? ParametersJson { get; init; }

    public bool IsEnabled { get; init; }
}

public sealed class ScheduledTaskResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string JobType { get; init; } = string.Empty;

    public string CronExpression { get; init; } = string.Empty;

    public string Queue { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? ParametersJson { get; init; }

    public bool IsEnabled { get; init; }

    public DateTimeOffset? LastRunAt { get; init; }

    public bool? LastRunSucceeded { get; init; }

    public string? LastRunMessage { get; init; }

    public string? LastJobId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ScheduledTaskExecutionLogResponse
{
    public Guid Id { get; init; }

    public Guid ScheduledTaskId { get; init; }

    public string? JobId { get; init; }

    public string JobType { get; init; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public bool Succeeded { get; init; }

    public string? TraceId { get; init; }

    public string? Message { get; init; }
}

public interface IScheduledTaskService
{
    Task<PagedResult<ScheduledTaskResponse>> GetPagedAsync(ScheduledTaskQueryRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<ScheduledTaskExecutionLogResponse>> GetLogsAsync(Guid taskId, ScheduledTaskLogQueryRequest request, CancellationToken cancellationToken = default);

    Task<ScheduledTaskResponse> CreateAsync(CreateScheduledTaskRequest request, CancellationToken cancellationToken = default);

    Task<ScheduledTaskResponse> UpdateAsync(Guid id, UpdateScheduledTaskRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task EnableAsync(Guid id, CancellationToken cancellationToken = default);

    Task DisableAsync(Guid id, CancellationToken cancellationToken = default);

    Task TriggerAsync(Guid id, CancellationToken cancellationToken = default);

    Task SyncEnabledTasksAsync(CancellationToken cancellationToken = default);
}
