using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Jobs;

public sealed class JobInfoService : IJobInfoService
{
    private readonly IRepository<ScheduledTask> _taskRepository;
    private readonly IRepository<JobExecutionLog> _logRepository;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly IMessageBus _messageBus;

    public JobInfoService(
        IRepository<ScheduledTask> taskRepository,
        IRepository<JobExecutionLog> logRepository,
        IBackgroundJobService backgroundJobService,
        IScheduledTaskService scheduledTaskService,
        IMessageBus messageBus)
    {
        _taskRepository = taskRepository;
        _logRepository = logRepository;
        _backgroundJobService = backgroundJobService;
        _scheduledTaskService = scheduledTaskService;
        _messageBus = messageBus;
    }

    public Task<PagedResult<JobInfoResponse>> GetPagedAsync(
        JobInfoQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var latestLogs = _logRepository.Query()
            .OrderByDescending(entity => entity.StartedAt)
            .ToList()
            .GroupBy(entity => entity.JobName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var jobs = _taskRepository.Query()
            .OrderByDescending(entity => entity.CreatedAt)
            .ToList()
            .Select(task => ToJobInfo(task, latestLogs.GetValueOrDefault(task.Code)))
            .Append(ToOutboxJobInfo(latestLogs.GetValueOrDefault(JobNames.OutboxPublisher), _messageBus.IsOutboxPublisherEnabled))
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            jobs = jobs
                .Where(job =>
                    job.JobName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    job.JobType.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    job.Source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            jobs = jobs
                .Where(job => string.Equals(job.Status, status, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalCount = jobs.Count;
        var items = jobs
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList();

        return Task.FromResult(PagedResult<JobInfoResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public Task<PagedResult<JobExecutionLogResponse>> GetLogsAsync(
        JobExecutionLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _logRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.JobName.Contains(keyword) ||
                (entity.JobId != null && entity.JobId.Contains(keyword)) ||
                (entity.TraceId != null && entity.TraceId.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.JobName))
        {
            var jobName = request.JobName.Trim();
            query = query.Where(entity => entity.JobName == jobName);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(entity => entity.Status == status);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.StartedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToLogResponse)
            .ToList();

        return Task.FromResult(PagedResult<JobExecutionLogResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task TriggerAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var task = ResolveScheduledTask(jobName);
        if (task is not null)
        {
            await _scheduledTaskService.TriggerAsync(task.Id, cancellationToken);
            return;
        }

        if (IsOutboxPublisher(jobName))
        {
            if (!_messageBus.IsOutboxPublisherEnabled)
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "RabbitMQ outbox publisher is disabled.");
            }

            _backgroundJobService.Enqueue<OutboxPublisherJob>(job => job.ExecuteAsync());
            return;
        }

        throw new BusinessException(ErrorCode.NotFound, "Job was not found.");
    }

    public async Task EnableAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var task = ResolveScheduledTask(jobName);
        if (task is null)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Enable operation is reserved for built-in jobs.");
        }

        await _scheduledTaskService.EnableAsync(task.Id, cancellationToken);
    }

    public async Task DisableAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var task = ResolveScheduledTask(jobName);
        if (task is null)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Disable operation is reserved for built-in jobs.");
        }

        await _scheduledTaskService.DisableAsync(task.Id, cancellationToken);
    }

    private ScheduledTask? ResolveScheduledTask(string jobName)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            return null;
        }

        var normalized = jobName.Trim();
        return _taskRepository.Query()
            .ToList()
            .FirstOrDefault(task =>
                string.Equals(task.Code, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(task.Name, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(task.Id.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ScheduledTaskService.GetRecurringJobId(task.Id), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOutboxPublisher(string jobName)
    {
        return string.Equals(jobName.Trim(), JobNames.OutboxPublisher, StringComparison.OrdinalIgnoreCase);
    }

    private static JobInfoResponse ToJobInfo(ScheduledTask task, JobExecutionLog? latestLog)
    {
        return new JobInfoResponse
        {
            JobName = task.Code,
            JobId = ScheduledTaskService.GetRecurringJobId(task.Id),
            JobType = task.JobType,
            Source = "ScheduledTask",
            Queue = task.Queue,
            CronExpression = task.CronExpression,
            IsEnabled = task.IsEnabled,
            Status = task.IsEnabled ? "Enabled" : "Disabled",
            LastRunAt = latestLog?.FinishedAt ?? task.LastRunAt,
            LastRunStatus = latestLog?.Status ?? ToLastRunStatus(task.LastRunSucceeded),
            LastJobId = latestLog?.JobId ?? task.LastJobId,
            LastErrorMessage = latestLog?.ErrorMessage ?? (task.LastRunSucceeded == false ? task.LastRunMessage : null)
        };
    }

    private static JobInfoResponse ToOutboxJobInfo(JobExecutionLog? latestLog, bool isEnabled)
    {
        return new JobInfoResponse
        {
            JobName = JobNames.OutboxPublisher,
            JobId = JobNames.OutboxPublisher,
            JobType = nameof(OutboxPublisherJob),
            Source = "System",
            Queue = "default",
            CronExpression = "* * * * *",
            IsEnabled = isEnabled,
            Status = isEnabled ? "Enabled" : "Disabled",
            LastRunAt = latestLog?.FinishedAt,
            LastRunStatus = latestLog?.Status,
            LastJobId = latestLog?.JobId,
            LastErrorMessage = latestLog?.ErrorMessage
        };
    }

    private static string? ToLastRunStatus(bool? succeeded)
    {
        return succeeded switch
        {
            true => JobExecutionStatuses.Succeeded,
            false => JobExecutionStatuses.Failed,
            _ => null
        };
    }

    private static JobExecutionLogResponse ToLogResponse(JobExecutionLog log)
    {
        return new JobExecutionLogResponse
        {
            Id = log.Id,
            TenantId = log.TenantId,
            JobName = log.JobName,
            JobId = log.JobId,
            Status = log.Status,
            StartedAt = log.StartedAt,
            FinishedAt = log.FinishedAt,
            ElapsedMilliseconds = log.ElapsedMilliseconds,
            ErrorMessage = log.ErrorMessage,
            TraceId = log.TraceId
        };
    }
}
