using System.Linq.Expressions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Infrastructure.BackgroundJobs;

public sealed class DisabledBackgroundJobService : IBackgroundJobService
{
    public bool IsEnabled => false;

    public string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall) => throw CreateDisabledException();

    public string Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay) => throw CreateDisabledException();

    public void AddOrUpdateRecurring<TJob>(
        string recurringJobId,
        Expression<Func<TJob, Task>> methodCall,
        string cronExpression,
        TimeZoneInfo? timeZone = null,
        string queue = "default") => throw CreateDisabledException();

    public void RemoveRecurring(string recurringJobId)
    {
    }

    public void TriggerRecurring(string recurringJobId) => throw CreateDisabledException();

    public bool Delete(string jobId) => false;

    private static BusinessException CreateDisabledException() => new(
        ErrorCode.ValidationFailed,
        "Hangfire background jobs are disabled.");
}
