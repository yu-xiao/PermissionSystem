using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiAnomalies;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiAnomalyExecutionHost(IServiceScopeFactory scopes, ILogger<AiAnomalyExecutionHost> logger)
    : IAiAnomalyExecutionHost, INotificationSourceHandler
{
    public string SourceKind => AiAnomalyContract.SourceKind;
    public Task HandleAsync(Guid tenantId, Guid sourceId, CancellationToken ct) => DeliverAsync(tenantId, sourceId, ct);
    public async Task RecordSkippedAsync(Guid taskId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        using var system = provider.GetRequiredService<ISystemTenantScope>().Begin(SystemTenantOperations.ScheduledTaskExecution);
        var task = await provider.GetRequiredService<IAsyncQueryExecutor>().FirstOrDefaultAsync(
            provider.GetRequiredService<IRepository<ScheduledTask>>().Query().Where(t => t.Id == taskId && t.JobType == AiAnomalyContract.JobType), ct);
        if (task is not null) await RecordFailure(task.TenantId, taskId, "lock_held_skipped");
    }

    public async Task ExecuteTaskAsync(Guid taskId, CancellationToken ct)
    {
        Guid? tenantId = null;
        try
        {
            await using var discovery = scopes.CreateAsyncScope();
            var provider = discovery.ServiceProvider;
            AiAnomalyRule? rule;
            using (provider.GetRequiredService<ISystemTenantScope>().Begin(SystemTenantOperations.ScheduledTaskExecution))
            {
                var query = provider.GetRequiredService<IAsyncQueryExecutor>();
                var task = await query.FirstOrDefaultAsync(provider.GetRequiredService<IRepository<ScheduledTask>>().Query()
                    .Where(t => t.Id == taskId && t.JobType == AiAnomalyContract.JobType), ct);
                if (task is null) return;
                tenantId = task.TenantId;
                rule = await query.FirstOrDefaultAsync(provider.GetRequiredService<IRepository<AiAnomalyRule>>().Query()
                    .Where(r => r.TenantId == task.TenantId && r.ScheduledTaskId == taskId), ct);
            }
            if (rule is null) throw new BusinessException(ErrorCode.ValidationFailed, "Reminder task is unbound.");
            await using var execution = scopes.CreateAsyncScope();
            await SetActor(execution.ServiceProvider, rule.TenantId, rule.OwnerUserId, ct);
            await execution.ServiceProvider.GetRequiredService<AiAnomalyExecutionService>().CheckAsync(rule.Id, ct);
        }
        catch (Exception ex)
        {
            if (tenantId.HasValue) await RecordFailure(tenantId.Value, taskId, FailureCode(ex));
            logger.LogWarning("Demo reminder task stopped. TaskId={TaskId} Code={Code}", taskId, FailureCode(ex));
            if (ex is OperationCanceledException && ct.IsCancellationRequested) throw;
        }
    }

    public async Task DeliverAsync(Guid tenantId, Guid eventId, CancellationToken ct)
    {
        Guid? ruleId = null;
        try
        {
            await using var discovery = scopes.CreateAsyncScope();
            var provider = discovery.ServiceProvider;
            provider.GetRequiredService<ITenantContext>().SetTenant(tenantId, "Message");
            var query = provider.GetRequiredService<IAsyncQueryExecutor>();
            var item = await query.FirstOrDefaultAsync(provider.GetRequiredService<IRepository<AiAnomalyEvent>>().Query()
                .Where(e => e.Id == eventId && e.TenantId == tenantId), ct);
            if (item is null) throw new BusinessException(ErrorCode.NotFound, "Reminder source is unavailable.");
            ruleId = item.RuleId;
            await using var execution = scopes.CreateAsyncScope();
            await SetActor(execution.ServiceProvider, tenantId, item.RecipientUserId, ct);
            await execution.ServiceProvider.GetRequiredService<AiAnomalyExecutionService>().DeliverQueuedAsync(item.RuleId, item.Id, ct);
        }
        catch (BusinessException ex) when (ruleId.HasValue && ex.ErrorCode is ErrorCode.Unauthorized or ErrorCode.Forbidden)
        {
            await using var suppressed = scopes.CreateAsyncScope();
            suppressed.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId, "Message");
            await suppressed.ServiceProvider.GetRequiredService<AiAnomalyExecutionService>().SuppressQueuedAsync(ruleId.Value, eventId, ct);
        }
    }

    private static async Task SetActor(IServiceProvider provider, Guid tenantId, Guid owner, CancellationToken ct)
    {
        var tenant = provider.GetRequiredService<ITenantContext>();
        tenant.SetTenant(tenantId, "Request");
        var actor = await provider.GetRequiredService<IUserCredentialValidator>().GetAuthenticationStateAsync(tenantId, owner, ct);
        if (actor is null || actor.TenantId != tenantId || actor.UserId != owner)
            throw new BusinessException(ErrorCode.Unauthorized, "Reminder owner is inactive.");
        var identity = provider.GetRequiredService<AiRunExecutionIdentity>();
        identity.Set(actor, string.Empty);
        tenant.MarkAsSuperAdmin(identity.IsSuperAdmin);
    }

    private async Task RecordFailure(Guid tenantId, Guid taskId, string code)
    {
        try
        {
            await using var recovery = scopes.CreateAsyncScope();
            var provider = recovery.ServiceProvider;
            provider.GetRequiredService<ITenantContext>().SetTenant(tenantId, "Request");
            var repository = provider.GetRequiredService<IRepository<ScheduledTask>>();
            var task = await provider.GetRequiredService<IAsyncQueryExecutor>().FirstOrDefaultAsync(repository.Query()
                .Where(t => t.Id == taskId && t.TenantId == tenantId && t.JobType == AiAnomalyContract.JobType), default);
            if (task is null) return;
            var now = DateTimeOffset.UtcNow;
            task.LastRunAt = now; task.LastRunSucceeded = false; task.LastRunMessage = code; repository.Update(task);
            await provider.GetRequiredService<IRepository<ScheduledTaskExecutionLog>>().AddAsync(new()
            { TenantId = tenantId, ScheduledTaskId = taskId, JobType = AiAnomalyContract.JobType, StartedAt = now,
                FinishedAt = now, Succeeded = false, Message = code });
            await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }
        catch { logger.LogWarning("Demo reminder failure log unavailable. TaskId={TaskId}", taskId); }
    }
    private static string FailureCode(Exception ex) => ex switch
    { OperationCanceledException => "cancelled_or_timeout", BusinessException b => $"rule_error_{(int)b.ErrorCode}", _ => "execution_failed" };
}
