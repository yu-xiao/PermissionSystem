using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiAnomalies;

public sealed class AiAnomalyService(IRepository<AiAnomalyRule> rules, IRepository<AiAnomalyEvent> events,
    IRepository<ScheduledTask> tasks, IRepository<OutboxMessage> outbox, IRepository<InboxMessage> inbox,
    AiAnomalyAccessPolicy access, IDemoBusinessOrderReadOnlyQueryService query, IAsyncQueryExecutor queries,
    IUnitOfWork unit, IAiAnomalyCommitFence fence, IBackgroundJobService jobs, INotificationService notifications,
    TimeProvider clock) : IAiAnomalyService
{
    public async Task<PagedResult<AiAnomalyRuleResponse>> ListAsync(int pageIndex, int pageSize, CancellationToken ct = default)
    {
        ValidatePage(pageIndex, pageSize);
        var actor = await access.RequireAsync(AiAnomalyContract.ViewPermission, false, ct);
        var source = rules.Query().Where(r => r.TenantId == actor.TenantId && r.OwnerUserId == actor.UserId);
        var count = await queries.LongCountAsync(source, ct);
        var rows = await queries.ToListAsync(source.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize), ct);
        var result = new List<AiAnomalyRuleResponse>();
        foreach (var rule in rows) result.Add(await ToResponse(rule, ct));
        await access.RequireAsync(AiAnomalyContract.ViewPermission, false, ct);
        return PagedResult<AiAnomalyRuleResponse>.Create(result, pageIndex, pageSize, count);
    }

    public async Task<AiAnomalyRuleResponse> CreateAsync(CancellationToken ct = default)
    {
        var actor = await access.RequireAsync(AiAnomalyContract.CreatePermission, true, ct);
        if (await queries.AnyAsync(rules.Query().Where(r => r.TenantId == actor.TenantId && r.OwnerUserId == actor.UserId), ct))
            throw new BusinessException(ErrorCode.Conflict, "Only one fixed Demo reminder per user is supported.");
        var task = new ScheduledTask { Id = Guid.NewGuid(), TenantId = actor.TenantId, Code = $"aic011-{actor.UserId:N}",
            Name = "Demo 待审批提醒", JobType = AiAnomalyContract.JobType, CronExpression = AiAnomalyContract.Cron,
            IsEnabled = false, Queue = "default" };
        var rule = new AiAnomalyRule { Id = Guid.NewGuid(), TenantId = actor.TenantId,
            OwnerUserId = actor.UserId, ScheduledTaskId = task.Id };
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await access.RequireAsync(AiAnomalyContract.CreatePermission, true, token);
            await tasks.AddAsync(task, token); await rules.AddAsync(rule, token); await unit.SaveChangesAsync(token);
        }, ct);
        return await ToResponse(rule, ct);
    }

    public async Task<AiAnomalyRuleResponse> SetEnabledAsync(Guid id, AiAnomalyChangeRequest request, CancellationToken ct = default)
    {
        await access.RequireAsync(AiAnomalyContract.UpdatePermission, request.IsEnabled, ct);
        if (request.IsEnabled)
        {
            EnsureJobs(); await access.RequireExecutionAsync(ct);
            if (!notifications.GetDeliveryStatus().IsEnabled)
                throw new BusinessException(ErrorCode.ValidationFailed, "Station notification delivery is disabled.");
        }
        AiAnomalyRule? updated = null;
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await fence.HoldAsync(id, token);
            var rule = await OwnRule(id, AiAnomalyContract.UpdatePermission, request.IsEnabled, token);
            RequireToken(rule, request.RowVersion);
            var task = await TaskFor(rule, token);
            if (request.IsEnabled) await access.RequireExecutionAsync(token);
            rule.IsEnabled = task.IsEnabled = request.IsEnabled;
            rules.Update(rule); tasks.Update(task); await unit.SaveChangesAsync(token); updated = rule;
        }, ct);
        if (request.IsEnabled) AiAnomalyScheduling.Register(await TaskFor(updated!, ct), jobs);
        else jobs.RemoveRecurring(ScheduledTaskService.GetRecurringJobId(updated!.ScheduledTaskId));
        return await ToResponse(updated!, ct);
    }

    public async Task TriggerAsync(Guid id, CancellationToken ct = default)
    {
        EnsureJobs();
        var rule = await OwnRule(id, AiAnomalyContract.TriggerPermission, true, ct);
        await access.RequireExecutionAsync(ct);
        var task = await TaskFor(rule, ct);
        if (!rule.IsEnabled || !task.IsEnabled) throw new BusinessException(ErrorCode.ValidationFailed, "Enable the reminder before checking.");
        jobs.Enqueue<AiAnomalyScheduledJob>(job => job.ExecuteAsync(task.Id, CancellationToken.None));
    }

    public async Task<PagedResult<AiAnomalyEventResponse>> EventsAsync(Guid ruleId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        ValidatePage(pageIndex, pageSize);
        await OwnRule(ruleId, AiAnomalyContract.ViewPermission, false, ct);
        PagedResult<AiAnomalyEventResponse>? result = null;
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await fence.HoldAsync(ruleId, token);
            var rule = await OwnRule(ruleId, AiAnomalyContract.ViewPermission, false, token);
            var source = events.Query().Where(e => e.TenantId == rule.TenantId && e.RuleId == rule.Id && e.RecipientUserId == rule.OwnerUserId);
            var count = await queries.LongCountAsync(source, token);
            var rows = await queries.ToListAsync(source.OrderByDescending(e => e.EpisodeSequence)
                .Skip((pageIndex - 1) * pageSize).Take(pageSize), token);
            var scope = await CurrentFingerprint(token);
            var responses = new List<AiAnomalyEventResponse>();
            foreach (var row in rows) responses.Add(await EventResponse(row, scope, token));
            if (scope != await CurrentFingerprint(token)) throw new BusinessException(ErrorCode.Forbidden, "Reminder evidence scope changed.");
            await access.RequireAsync(AiAnomalyContract.ViewPermission, false, token);
            result = PagedResult<AiAnomalyEventResponse>.Create(responses, pageIndex, pageSize, count);
        }, ct);
        return result!;
    }

    public async Task<AiAnomalyEventResponse> EventAsync(Guid eventId, CancellationToken ct = default)
    {
        var actor = await access.RequireAsync(AiAnomalyContract.ViewPermission, false, ct);
        var item = await queries.FirstOrDefaultAsync(events.Query().Where(e => e.TenantId == actor.TenantId &&
            e.RecipientUserId == actor.UserId && e.Id == eventId), ct) ?? throw Missing();
        AiAnomalyEventResponse? result = null;
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await fence.HoldAsync(item.RuleId, token);
            item = await queries.FirstOrDefaultAsync(events.Query().Where(e => e.TenantId == actor.TenantId &&
                e.RecipientUserId == actor.UserId && e.Id == eventId), token) ?? throw Missing();
            await OwnRule(item.RuleId, AiAnomalyContract.ViewPermission, false, token);
            var scope = await CurrentFingerprint(token);
            result = await EventResponse(item, scope, token);
            if (scope != await CurrentFingerprint(token)) throw new BusinessException(ErrorCode.Forbidden, "Reminder evidence scope changed.");
            await access.RequireAsync(AiAnomalyContract.ViewPermission, false, token);
        }, ct);
        return result!;
    }

    public async Task CloseAsync(Guid eventId, byte[] rowVersion, CancellationToken ct = default)
    {
        var actor = await access.RequireAsync(AiAnomalyContract.UpdatePermission, false, ct);
        var item = await queries.FirstOrDefaultAsync(events.Query().Where(e => e.TenantId == actor.TenantId &&
            e.RecipientUserId == actor.UserId && e.Id == eventId), ct) ?? throw Missing();
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await fence.HoldAsync(item.RuleId, token);
            var rule = await OwnRule(item.RuleId, AiAnomalyContract.UpdatePermission, false, token);
            item = await queries.FirstOrDefaultAsync(events.Query().Where(e => e.Id == eventId && e.TenantId == actor.TenantId), token) ?? throw Missing();
            RequireToken(item, rowVersion);
            item.Close(clock.GetUtcNow(), "manual");
            if (rule.ReservedEventId == item.Id) rule.ReservedEventId = null;
            events.Update(item); rules.Update(rule); await unit.SaveChangesAsync(token);
        }, ct);
    }

    private async Task<string> CurrentFingerprint(CancellationToken ct) =>
        AiAnomalyContract.Fingerprint((await query.QueryAsync(new() { ApprovalStatus = "Pending", Limit = 1 }, ct)).Scope);

    private async Task<AiAnomalyEventResponse> EventResponse(AiAnomalyEvent item, string scope, CancellationToken ct)
    {
        string? transport = null;
        if (item.MessageId is string id)
        {
            var sent = await queries.FirstOrDefaultAsync(outbox.Query().Where(m => m.TenantId == item.TenantId && m.MessageId == id), ct);
            var consumed = await queries.FirstOrDefaultAsync(inbox.Query().Where(m => m.TenantId == item.TenantId && m.MessageId == id &&
                m.Consumer == NotificationMessageNames.QueueName), ct);
            transport = $"Outbox:{sent?.Status.ToString() ?? "Unavailable"};Inbox:{consumed?.Status.ToString() ?? "Awaiting"}";
        }
        var unavailable = scope != item.ScopeFingerprint;
        return new(item.Id, item.RuleId, item.EpisodeSequence, unavailable ? null : item.ObservedCount, item.ObservedAt,
            item.ClosedAt, item.CloseReason, item.DeliveryStatus.ToString(), item.AttemptCount, item.NextAttemptAt, item.ErrorCode,
            item.RowVersion, unavailable, transport);
    }

    private async Task<AiAnomalyRule> OwnRule(Guid id, string permission, bool enabled, CancellationToken ct)
    {
        var actor = await access.RequireAsync(permission, enabled, ct);
        return await queries.FirstOrDefaultAsync(rules.Query().Where(r => r.Id == id && r.TenantId == actor.TenantId &&
            r.OwnerUserId == actor.UserId && r.RuleType == "DemoPendingCount" && r.ContractVersion == 1), ct) ?? throw Missing();
    }
    private async Task<ScheduledTask> TaskFor(AiAnomalyRule rule, CancellationToken ct) =>
        await queries.FirstOrDefaultAsync(tasks.Query().Where(t => t.Id == rule.ScheduledTaskId && t.TenantId == rule.TenantId &&
            t.JobType == AiAnomalyContract.JobType), ct) ?? throw Missing();
    private async Task<AiAnomalyRuleResponse> ToResponse(AiAnomalyRule rule, CancellationToken ct)
    {
        var task = await TaskFor(rule, ct);
        return new(rule.Id, rule.IsEnabled, rule.LastNotifiedAt, task.LastRunAt,
            task.LastRunSucceeded, task.LastRunMessage, rule.RowVersion);
    }
    private void EnsureJobs() { if (!jobs.IsEnabled) throw new BusinessException(ErrorCode.ValidationFailed, "Hangfire jobs are disabled."); }
    private static BusinessException Missing() => new(ErrorCode.NotFound, "Reminder resource was not found.");
    internal static void RequireToken(PermissionSystem.Domain.Common.BaseEntity item, byte[] token)
    {
        if (token.Length != 8) throw new BusinessException(ErrorCode.ValidationFailed, "An 8-byte rowversion is required.");
        ConcurrencyTokenGuard.EnsureMatches(item, token);
    }
    private static void ValidatePage(int page, int size)
    { if (page is < 1 or > 100000 || size is < 1 or > 100) throw new BusinessException(ErrorCode.ValidationFailed, "Invalid reminder pagination."); }
}

public static class AiAnomalyScheduling
{
    public static void Register(ScheduledTask task, IBackgroundJobService jobs)
    {
        if (task.JobType != AiAnomalyContract.JobType || task.CronExpression != AiAnomalyContract.Cron ||
            task.Queue != "default" || task.ParametersJson is not null)
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid controlled reminder task.");
        jobs.AddOrUpdateRecurring<AiAnomalyScheduledJob>(ScheduledTaskService.GetRecurringJobId(task.Id),
            job => job.ExecuteAsync(task.Id, CancellationToken.None), AiAnomalyContract.Cron, AiAnomalyContract.TimeZone);
    }
}
