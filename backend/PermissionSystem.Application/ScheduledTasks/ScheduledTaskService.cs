using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.ScheduledTasks;

public sealed class ScheduledTaskService : IScheduledTaskService
{
    private readonly IRepository<ScheduledTask> _taskRepository;
    private readonly IRepository<ScheduledTaskExecutionLog> _logRepository;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemTenantScope _systemTenantScope;
    private readonly ITenantStatusChecker _tenantStatusChecker;

    public ScheduledTaskService(
        IRepository<ScheduledTask> taskRepository,
        IRepository<ScheduledTaskExecutionLog> logRepository,
        IBackgroundJobService backgroundJobService,
        ITenantWriteResolver tenantWriteResolver,
        IUnitOfWork unitOfWork,
        ISystemTenantScope systemTenantScope,
        ITenantStatusChecker tenantStatusChecker)
    {
        _taskRepository = taskRepository;
        _logRepository = logRepository;
        _backgroundJobService = backgroundJobService;
        _tenantWriteResolver = tenantWriteResolver;
        _unitOfWork = unitOfWork;
        _systemTenantScope = systemTenantScope;
        _tenantStatusChecker = tenantStatusChecker;
    }

    public Task<PagedResult<ScheduledTaskResponse>> GetPagedAsync(
        ScheduledTaskQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _taskRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.Code.Contains(keyword) ||
                entity.Name.Contains(keyword) ||
                entity.JobType.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.JobType))
        {
            var jobType = request.JobType.Trim();
            query = query.Where(entity => entity.JobType == jobType);
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<ScheduledTaskResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public Task<PagedResult<ScheduledTaskExecutionLogResponse>> GetLogsAsync(
        Guid taskId,
        ScheduledTaskLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _logRepository.Query().Where(entity => entity.ScheduledTaskId == taskId);
        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.StartedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToLogResponse)
            .ToList();

        return Task.FromResult(PagedResult<ScheduledTaskExecutionLogResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<ScheduledTaskResponse> CreateAsync(
        CreateScheduledTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Code, "Task code is required.");
        ValidateRequest(request.Name, request.JobType, request.CronExpression, request.Queue);

        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        var code = request.Code.Trim();
        if (_taskRepository.Query().Any(entity => entity.TenantId == tenantId && entity.Code == code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Task code already exists.");
        }

        var task = new ScheduledTask
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            JobType = request.JobType.Trim(),
            CronExpression = request.CronExpression.Trim(),
            Queue = NormalizeQueue(request.Queue),
            Description = request.Description,
            ParametersJson = NormalizeJson(request.ParametersJson),
            IsEnabled = request.IsEnabled
        };

        await _taskRepository.AddAsync(task, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        SyncHangfireJob(task);

        return ToResponse(task);
    }

    public async Task<ScheduledTaskResponse> UpdateAsync(
        Guid id,
        UpdateScheduledTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Name, request.JobType, request.CronExpression, request.Queue);

        var task = await GetTaskOrThrowAsync(id, cancellationToken);
        EnsureSupportedJobType(task.JobType);
        ConcurrencyTokenGuard.EnsureMatches(task, request.ConcurrencyToken);
        task.Name = request.Name.Trim();
        task.JobType = request.JobType.Trim();
        task.CronExpression = request.CronExpression.Trim();
        task.Queue = NormalizeQueue(request.Queue);
        task.Description = request.Description;
        task.ParametersJson = NormalizeJson(request.ParametersJson);
        task.IsEnabled = request.IsEnabled;

        _taskRepository.Update(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        SyncHangfireJob(task);

        return ToResponse(task);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskOrThrowAsync(id, cancellationToken);
        _backgroundJobService.RemoveRecurring(GetRecurringJobId(task.Id));
        _taskRepository.Remove(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task EnableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskOrThrowAsync(id, cancellationToken);
        EnsureSupportedJobType(task.JobType);
        task.IsEnabled = true;
        _taskRepository.Update(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        SyncHangfireJob(task);
    }

    public async Task DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskOrThrowAsync(id, cancellationToken);
        task.IsEnabled = false;
        _taskRepository.Update(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _backgroundJobService.RemoveRecurring(GetRecurringJobId(task.Id));
    }

    public async Task TriggerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskOrThrowAsync(id, cancellationToken);
        EnsureSupportedJobType(task.JobType);
        SyncHangfireJob(task);
        _backgroundJobService.TriggerRecurring(GetRecurringJobId(task.Id));
    }

    public async Task SyncEnabledTasksAsync(CancellationToken cancellationToken = default)
    {
        using var systemScope = _systemTenantScope.Begin(SystemTenantOperations.ScheduledTaskSynchronization);
        foreach (var task in _taskRepository.Query().Where(entity => entity.IsEnabled).ToList())
        {
            if (!ScheduledTaskJobTypes.IsSupported(task.JobType))
            {
                _backgroundJobService.RemoveRecurring(GetRecurringJobId(task.Id));
                continue;
            }

            if (await _tenantStatusChecker.IsActiveAsync(task.TenantId, cancellationToken))
            {
                SyncHangfireJob(task);
            }
            else
            {
                _backgroundJobService.RemoveRecurring(GetRecurringJobId(task.Id));
            }
        }
    }

    public Task SuspendTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        foreach (var task in _taskRepository.QueryForTenant(tenantId).Where(entity => entity.IsEnabled).ToList())
        {
            _backgroundJobService.RemoveRecurring(GetRecurringJobId(task.Id));
        }

        return Task.CompletedTask;
    }

    public Task ResumeTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        foreach (var task in _taskRepository.QueryForTenant(tenantId).Where(entity => entity.IsEnabled).ToList())
        {
            if (ScheduledTaskJobTypes.IsSupported(task.JobType))
            {
                SyncHangfireJob(task);
            }
            else
            {
                _backgroundJobService.RemoveRecurring(GetRecurringJobId(task.Id));
            }
        }

        return Task.CompletedTask;
    }

    private async Task<ScheduledTask> GetTaskOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _taskRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Scheduled task was not found.");
    }

    private void SyncHangfireJob(ScheduledTask task)
    {
        var recurringJobId = GetRecurringJobId(task.Id);

        if (!task.IsEnabled)
        {
            _backgroundJobService.RemoveRecurring(recurringJobId);
            return;
        }

        EnsureSupportedJobType(task.JobType);

        _backgroundJobService.AddOrUpdateRecurring<DemoScheduledTaskJob>(
            recurringJobId,
            job => job.ExecuteAsync(task.Id),
            task.CronExpression,
            TimeZoneInfo.Local,
            task.Queue);
    }

    public static string GetRecurringJobId(Guid taskId)
    {
        return $"scheduled-task:{taskId:N}";
    }

    private static void ValidateRequest(string name, string jobType, string cronExpression, string queue)
    {
        ValidateRequired(name, "Task name is required.");
        ValidateRequired(jobType, "Task job type is required.");
        ValidateRequired(cronExpression, "Cron expression is required.");
        ValidateRequired(queue, "Task queue is required.");

        EnsureSupportedJobType(jobType);

        var cronParts = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (cronParts.Length != 5)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Cron expression must contain 5 fields.");
        }
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }

    private static void EnsureSupportedJobType(string jobType)
    {
        if (!ScheduledTaskJobTypes.IsSupported(jobType))
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "Only the controlled DemoLog job type is available; custom production jobs are reserved.");
        }
    }

    private static string NormalizeQueue(string queue)
    {
        return string.IsNullOrWhiteSpace(queue) ? "default" : queue.Trim().ToLowerInvariant();
    }

    private static string? NormalizeJson(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ScheduledTaskResponse ToResponse(ScheduledTask task)
    {
        return new ScheduledTaskResponse
        {
            Id = task.Id,
            TenantId = task.TenantId,
            Code = task.Code,
            Name = task.Name,
            JobType = task.JobType,
            CronExpression = task.CronExpression,
            Queue = task.Queue,
            Description = task.Description,
            ParametersJson = task.ParametersJson,
            IsEnabled = task.IsEnabled,
            LastRunAt = task.LastRunAt,
            LastRunSucceeded = task.LastRunSucceeded,
            LastRunMessage = task.LastRunMessage,
            LastJobId = task.LastJobId,
            CreatedAt = task.CreatedAt,
            ConcurrencyToken = task.RowVersion
        };
    }

    private static ScheduledTaskExecutionLogResponse ToLogResponse(ScheduledTaskExecutionLog log)
    {
        return new ScheduledTaskExecutionLogResponse
        {
            Id = log.Id,
            ScheduledTaskId = log.ScheduledTaskId,
            JobId = log.JobId,
            JobType = log.JobType,
            StartedAt = log.StartedAt,
            FinishedAt = log.FinishedAt,
            Succeeded = log.Succeeded,
            TraceId = log.TraceId,
            Message = log.Message
        };
    }
}
