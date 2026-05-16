using Microsoft.Extensions.Logging;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.ScheduledTasks;

public sealed class DemoScheduledTaskJob
{
    private readonly IRepository<ScheduledTask> _taskRepository;
    private readonly IRepository<ScheduledTaskExecutionLog> _logRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DemoScheduledTaskJob> _logger;

    public DemoScheduledTaskJob(
        IRepository<ScheduledTask> taskRepository,
        IRepository<ScheduledTaskExecutionLog> logRepository,
        IUnitOfWork unitOfWork,
        ILogger<DemoScheduledTaskJob> logger)
    {
        _taskRepository = taskRepository;
        _logRepository = logRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid taskId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId)
            ?? throw new BusinessException(ErrorCode.NotFound, "Scheduled task was not found.");

        var startedAt = DateTimeOffset.UtcNow;
        var message = $"Demo scheduled task '{task.Name}' executed at {startedAt:O}.";
        if (!string.IsNullOrWhiteSpace(task.ParametersJson))
        {
            message = $"{message} Parameters: {task.ParametersJson}";
        }

        var log = new ScheduledTaskExecutionLog
        {
            TenantId = task.TenantId,
            ScheduledTaskId = task.Id,
            JobType = task.JobType,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            Succeeded = true,
            Message = message,
            ParametersJson = task.ParametersJson
        };

        task.LastRunAt = log.FinishedAt;
        task.LastRunSucceeded = true;
        task.LastRunMessage = message;

        await _logRepository.AddAsync(log);
        _taskRepository.Update(task);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Demo scheduled task executed. TaskId: {TaskId}, TaskCode: {TaskCode}", task.Id, task.Code);
    }
}
