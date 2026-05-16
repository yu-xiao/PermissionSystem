using System.Linq.Expressions;

namespace PermissionSystem.Application.Abstractions;

public interface IBackgroundJobService
{
    string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall);

    string Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay);

    void AddOrUpdateRecurring<TJob>(
        string recurringJobId,
        Expression<Func<TJob, Task>> methodCall,
        string cronExpression,
        TimeZoneInfo? timeZone = null,
        string queue = "default");

    void RemoveRecurring(string recurringJobId);

    void TriggerRecurring(string recurringJobId);

    bool Delete(string jobId);
}
