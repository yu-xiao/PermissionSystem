using System.Linq.Expressions;
using Hangfire;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Infrastructure.BackgroundJobs;

public sealed class HangfireBackgroundJobService : IBackgroundJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;

    public HangfireBackgroundJobService(
        IBackgroundJobClient backgroundJobClient,
        IRecurringJobManager recurringJobManager)
    {
        _backgroundJobClient = backgroundJobClient;
        _recurringJobManager = recurringJobManager;
    }

    public bool IsEnabled => true;

    public string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall)
    {
        return _backgroundJobClient.Enqueue(methodCall);
    }

    public string Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay)
    {
        return _backgroundJobClient.Schedule(methodCall, delay);
    }

    public void AddOrUpdateRecurring<TJob>(
        string recurringJobId,
        Expression<Func<TJob, Task>> methodCall,
        string cronExpression,
        TimeZoneInfo? timeZone = null,
        string queue = "default")
    {
#pragma warning disable CS0618
        _recurringJobManager.AddOrUpdate(
            recurringJobId,
            methodCall,
            cronExpression,
            new RecurringJobOptions
            {
                TimeZone = timeZone ?? TimeZoneInfo.Local,
                QueueName = string.IsNullOrWhiteSpace(queue) ? "default" : queue
            });
#pragma warning restore CS0618
    }

    public bool Delete(string jobId)
    {
        return _backgroundJobClient.Delete(jobId);
    }

    public void RemoveRecurring(string recurringJobId)
    {
        _recurringJobManager.RemoveIfExists(recurringJobId);
    }

    public void TriggerRecurring(string recurringJobId)
    {
        _recurringJobManager.Trigger(recurringJobId);
    }
}
