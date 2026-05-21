using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Jobs;

public static class JobNames
{
    public const string OutboxPublisher = "outbox:publisher";
}

public static class JobExecutionStatuses
{
    public const string Succeeded = "Succeeded";

    public const string Failed = "Failed";

    public const string Skipped = "Skipped";
}

public sealed class JobInfoQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? Status { get; init; }
}

public sealed class JobExecutionLogQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? JobName { get; init; }

    public string? Status { get; init; }
}

public sealed class JobInfoResponse
{
    public string JobName { get; init; } = string.Empty;

    public string? JobId { get; init; }

    public string JobType { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Queue { get; init; } = string.Empty;

    public string? CronExpression { get; init; }

    public bool IsEnabled { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset? LastRunAt { get; init; }

    public string? LastRunStatus { get; init; }

    public string? LastJobId { get; init; }

    public string? LastErrorMessage { get; init; }
}

public sealed class JobExecutionLogResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string JobName { get; init; } = string.Empty;

    public string? JobId { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public long ElapsedMilliseconds { get; init; }

    public string? ErrorMessage { get; init; }

    public string? TraceId { get; init; }
}

public interface IJobInfoService
{
    Task<PagedResult<JobInfoResponse>> GetPagedAsync(JobInfoQueryRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<JobExecutionLogResponse>> GetLogsAsync(JobExecutionLogQueryRequest request, CancellationToken cancellationToken = default);

    Task TriggerAsync(string jobName, CancellationToken cancellationToken = default);

    Task EnableAsync(string jobName, CancellationToken cancellationToken = default);

    Task DisableAsync(string jobName, CancellationToken cancellationToken = default);
}
