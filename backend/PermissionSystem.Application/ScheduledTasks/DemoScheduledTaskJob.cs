using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Jobs;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.ScheduledTasks;

public sealed class DemoScheduledTaskJob
{
    private static readonly ActivitySource ActivitySource = new(TraceActivitySources.BackgroundJobs);

    private readonly IRepository<ScheduledTask> _taskRepository;
    private readonly IRepository<ScheduledTaskExecutionLog> _logRepository;
    private readonly IRepository<JobExecutionLog> _jobExecutionLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedLock _distributedLock;
    private readonly ITraceContextAccessor _traceContextAccessor;
    private readonly ILogger<DemoScheduledTaskJob> _logger;
    private readonly ISystemTenantScope _systemTenantScope;
    private readonly ITenantStatusChecker _tenantStatusChecker;

    public DemoScheduledTaskJob(
        IRepository<ScheduledTask> taskRepository,
        IRepository<ScheduledTaskExecutionLog> logRepository,
        IRepository<JobExecutionLog> jobExecutionLogRepository,
        IUnitOfWork unitOfWork,
        IDistributedLock distributedLock,
        ITraceContextAccessor traceContextAccessor,
        ILogger<DemoScheduledTaskJob> logger,
        ISystemTenantScope systemTenantScope,
        ITenantStatusChecker tenantStatusChecker)
    {
        _taskRepository = taskRepository;
        _logRepository = logRepository;
        _jobExecutionLogRepository = jobExecutionLogRepository;
        _unitOfWork = unitOfWork;
        _distributedLock = distributedLock;
        _traceContextAccessor = traceContextAccessor;
        _logger = logger;
        _systemTenantScope = systemTenantScope;
        _tenantStatusChecker = tenantStatusChecker;
    }

    public async Task ExecuteAsync(Guid taskId)
    {
        using var systemScope = _systemTenantScope.Begin(SystemTenantOperations.ScheduledTaskExecution);
        var traceId = EnsureTraceId();
        using var activity = StartJobActivity(traceId, $"hangfire.demo.{taskId:N}");
        using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var jobName = ScheduledTaskService.GetRecurringJobId(taskId);
        var tenantId = Guid.Empty;

        try
        {
            await _distributedLock.ExecuteWithLockAsync(
                $"hangfire:scheduled-task:{taskId:N}",
                async _ =>
                {
                    var task = await _taskRepository.GetByIdAsync(taskId)
                        ?? throw new BusinessException(ErrorCode.NotFound, "Scheduled task was not found.");
                    jobName = task.Code;
                    tenantId = task.TenantId;
                    if (!ScheduledTaskJobTypes.IsSupported(task.JobType))
                    {
                        throw new BusinessException(
                            ErrorCode.ValidationFailed,
                            "Only the controlled DemoLog job type is available; custom production jobs are reserved.");
                    }

                    if (!await _tenantStatusChecker.IsActiveAsync(task.TenantId))
                    {
                        _logger.LogInformation("Scheduled task skipped because tenant is not active. TaskId: {TaskId}, TenantId: {TenantId}", task.Id, task.TenantId);
                        return;
                    }

                    var taskStartedAt = DateTimeOffset.UtcNow;
                    var message = $"Demo scheduled task '{task.Name}' executed at {taskStartedAt:O}.";
                    if (!string.IsNullOrWhiteSpace(task.ParametersJson))
                    {
                        message = $"{message} Parameters: {task.ParametersJson}";
                    }

                    var log = new ScheduledTaskExecutionLog
                    {
                        TenantId = task.TenantId,
                        ScheduledTaskId = task.Id,
                        JobType = task.JobType,
                        StartedAt = taskStartedAt,
                        FinishedAt = DateTimeOffset.UtcNow,
                        Succeeded = true,
                        TraceId = traceId,
                        Message = message,
                        ParametersJson = task.ParametersJson
                    };

                    task.LastRunAt = log.FinishedAt;
                    task.LastRunSucceeded = true;
                    task.LastRunMessage = message;

                    await _logRepository.AddAsync(log);
                    _taskRepository.Update(task);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Demo scheduled task executed. TaskId: {TaskId}, TaskCode: {TaskCode}, TraceId: {TraceId}", task.Id, task.Code, traceId);
                },
                TimeSpan.FromMinutes(5),
                TimeSpan.Zero);

            await RecordJobExecutionLogAsync(
                tenantId,
                jobName,
                null,
                JobExecutionStatuses.Succeeded,
                startedAt,
                stopwatch,
                null,
                traceId);
        }
        catch (TimeoutException exception)
        {
            _logger.LogInformation("Demo scheduled task skipped because a distributed lock is held. TaskId: {TaskId}, TraceId: {TraceId}", taskId, traceId);
            await RecordJobExecutionLogAsync(
                tenantId,
                jobName,
                null,
                JobExecutionStatuses.Skipped,
                startedAt,
                stopwatch,
                exception.Message,
                traceId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Demo scheduled task failed. TaskId: {TaskId}, TraceId: {TraceId}", taskId, traceId);
            await RecordJobExecutionLogAsync(
                tenantId,
                jobName,
                null,
                JobExecutionStatuses.Failed,
                startedAt,
                stopwatch,
                exception.Message,
                traceId);
            throw;
        }
    }

    private async Task RecordJobExecutionLogAsync(
        Guid tenantId,
        string jobName,
        string? jobId,
        string status,
        DateTimeOffset startedAt,
        Stopwatch stopwatch,
        string? errorMessage,
        string traceId)
    {
        stopwatch.Stop();
        await _jobExecutionLogRepository.AddAsync(new JobExecutionLog
        {
            TenantId = tenantId,
            JobName = jobName,
            JobId = jobId,
            Status = status,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            ErrorMessage = Truncate(errorMessage, 2000),
            TraceId = traceId
        });

        await _unitOfWork.SaveChangesAsync();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
    }

    private string EnsureTraceId()
    {
        if (!string.IsNullOrWhiteSpace(_traceContextAccessor.TraceId))
        {
            return _traceContextAccessor.TraceId;
        }

        var traceId = ActivityTraceId.CreateRandom().ToString();
        _traceContextAccessor.TraceId = traceId;
        return traceId;
    }

    private static Activity? StartJobActivity(string traceId, string name)
    {
        var activity = ActivitySource.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("app.trace_id", traceId);
        activity?.SetTag("job.system", "hangfire");
        return activity;
    }
}
