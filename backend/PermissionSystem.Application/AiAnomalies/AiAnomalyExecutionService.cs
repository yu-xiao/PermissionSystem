using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiAnomalies;

public sealed class AiAnomalyExecutionService(IRepository<AiAnomalyRule> rules, IRepository<AiAnomalyEvent> events,
    IRepository<ScheduledTask> tasks, IRepository<ScheduledTaskExecutionLog> logs, IRepository<OutboxMessage> outbox,
    IRepository<DeadLetterMessage> deadLetters, AiAnomalyAccessPolicy access, IDemoBusinessOrderReadOnlyQueryService query,
    IAsyncQueryExecutor queries, IUnitOfWork unit, IAiAnomalyCommitFence fence, IControlledNotificationService notifications,
    INotificationService delivery, TimeProvider clock, ITraceContextAccessor trace)
{
    public async Task CheckAsync(Guid ruleId, CancellationToken ct)
    {
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await fence.HoldAsync(ruleId, token);
            var rule = await RuleAsync(ruleId, token);
            var task = await TaskAsync(rule, token);
            if (!rule.IsEnabled || !task.IsEnabled) { await Log(task, "paused", true, token); return; }
            var observation = await Observe(token);
            var prior = await CurrentEvent(rule, token);
            await ReconcileReservation(rule, token);
            if (observation.Data.TotalCount == 0)
            {
                rule.Observe(0);
                if (prior is not null) { prior.Close(clock.GetUtcNow(), "recovered"); events.Update(prior); }
                rule.ReservedEventId = null;
            }
            else if (rule.Observe(observation.Data.TotalCount))
            {
                var item = new AiAnomalyEvent { Id = Guid.NewGuid(), TenantId = rule.TenantId, RuleId = rule.Id,
                    RecipientUserId = rule.OwnerUserId, EpisodeSequence = rule.EpisodeSequence,
                    ObservedCount = observation.Data.TotalCount, ObservedAt = observation.QueriedAt,
                    ScopeFingerprint = AiAnomalyContract.Fingerprint(observation.Scope) };
                item.DeliveryKey = $"aic011-{item.Id:N}";
                await events.AddAsync(item, token);
            }
            else if (prior is not null && prior.ScopeFingerprint != AiAnomalyContract.Fingerprint(observation.Scope))
            {
                prior.Close(clock.GetUtcNow(), "scope_changed"); events.Update(prior);
                if (rule.ReservedEventId == prior.Id) rule.ReservedEventId = null;
            }
            rules.Update(rule);
            await Log(task, observation.Data.TotalCount == 0 ? "no_anomaly" : "checked", true, token);
        }, ct);

        var attempt = await ReserveAttempt(ruleId, ct);
        if (attempt is not null) await SendAttempt(ruleId, attempt.Value.Id, attempt.Value.Attempt, ct);
    }

    private async Task<(Guid Id, int Attempt)?> ReserveAttempt(Guid ruleId, CancellationToken ct)
    {
        (Guid Id, int Attempt)? attempt = null;
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await fence.HoldAsync(ruleId, token);
            var rule = await RuleAsync(ruleId, token);
            if (!rule.IsEnabled || !(await TaskAsync(rule, token)).IsEnabled) return;
            var item = await CurrentEvent(rule, token);
            var now = clock.GetUtcNow();
            if (!rule.IsAnomalous || item is null || !item.CanAttempt(now)) return;
            if (rule.IsCoolingDown(now)) item.DeliveryStatus = AiAnomalyDeliveryStatus.CoolingDown;
            else if (!delivery.GetDeliveryStatus().IsEnabled) item.DeliveryStatus = AiAnomalyDeliveryStatus.Disabled;
            else
            {
                await access.RequireExecutionAsync(token);
                item.AttemptCount++;
                item.RecordFailure(now, "delivery_interrupted");
                attempt = (item.Id, item.AttemptCount);
            }
            events.Update(item); await unit.SaveChangesAsync(token);
        }, ct);
        return attempt;
    }

    private async Task SendAttempt(Guid ruleId, Guid eventId, int attempt, CancellationToken ct)
    {
        string? pushKey = null;
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await fence.HoldAsync(ruleId, token);
            var rule = await RuleAsync(ruleId, token);
            var item = await EventAsync(rule, eventId, token);
            if (!rule.IsEnabled || !(await TaskAsync(rule, token)).IsEnabled || item.ClosedAt.HasValue ||
                item.AttemptCount != attempt || item.DeliveryStatus is AiAnomalyDeliveryStatus.Queued or AiAnomalyDeliveryStatus.Delivered)
                return;
            if (!await StillValid(rule, item, token)) { await Suppress(rule, item, "result_changed", token); return; }
            if (rule.IsCoolingDown(clock.GetUtcNow())) return;
            var receipt = await notifications.StageAsync(Request(rule, item), token);
            ApplyReceipt(rule, item, receipt);
            events.Update(item); rules.Update(rule); await unit.SaveChangesAsync(token);
            if (receipt.Status == NotificationDeliveryStatuses.Delivered) pushKey = item.DeliveryKey;
        }, ct);
        if (pushKey is not null) await notifications.PushAsync(pushKey, ct);
    }

    public async Task DeliverQueuedAsync(Guid ruleId, Guid eventId, CancellationToken ct)
    {
        string? pushKey = null;
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await fence.HoldAsync(ruleId, token);
            var rule = await RuleAsync(ruleId, token);
            var item = await EventAsync(rule, eventId, token);
            if (item.DeliveryStatus != AiAnomalyDeliveryStatus.Queued) return;
            if (!rule.IsEnabled || !(await TaskAsync(rule, token)).IsEnabled || item.ClosedAt.HasValue ||
                !delivery.GetDeliveryStatus().IsEnabled || !await StillValid(rule, item, token))
            { await Suppress(rule, item, "delivery_no_longer_authorized", token); return; }
            var receipt = await notifications.PersistAsync(Request(rule, item), token);
            ApplyReceipt(rule, item, receipt);
            events.Update(item); rules.Update(rule); await unit.SaveChangesAsync(token); pushKey = item.DeliveryKey;
        }, ct);
        if (pushKey is not null) await notifications.PushAsync(pushKey, ct);
    }

    public async Task SuppressQueuedAsync(Guid ruleId, Guid eventId, CancellationToken ct)
    {
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await fence.HoldAsync(ruleId, token);
            var rule = await queries.FirstOrDefaultAsync(rules.Query().Where(r => r.Id == ruleId), token);
            if (rule is null) return;
            var item = await queries.FirstOrDefaultAsync(events.Query().Where(e => e.Id == eventId && e.TenantId == rule.TenantId && e.RuleId == rule.Id), token);
            if (item?.DeliveryStatus == AiAnomalyDeliveryStatus.Queued)
                await Suppress(rule, item, "delivery_no_longer_authorized", token);
        }, ct);
    }

    private async Task<bool> StillValid(AiAnomalyRule rule, AiAnomalyEvent item, CancellationToken ct)
    {
        if (!rule.IsAnomalous || rule.EpisodeSequence != item.EpisodeSequence) return false;
        var observation = await Observe(ct);
        return observation.Data.TotalCount >= 1 && item.ScopeFingerprint == AiAnomalyContract.Fingerprint(observation.Scope);
    }
    private async Task<DemoBusinessOrderReadOnlyResult> Observe(CancellationToken ct)
    {
        await access.RequireExecutionAsync(ct);
        var result = await query.QueryAsync(new() { ApprovalStatus = "Pending", Limit = 1 }, ct);
        await access.RequireExecutionAsync(ct);
        return result;
    }
    private async Task<AiAnomalyRule> RuleAsync(Guid id, CancellationToken ct)
    {
        var actor = await access.RequireExecutionAsync(ct);
        return await queries.FirstOrDefaultAsync(rules.Query().Where(r => r.Id == id && r.TenantId == actor.TenantId &&
            r.OwnerUserId == actor.UserId && r.RuleType == "DemoPendingCount" && r.ContractVersion == 1), ct)
            ?? throw new BusinessException(ErrorCode.NotFound, "Reminder was not found.");
    }
    private async Task<ScheduledTask> TaskAsync(AiAnomalyRule rule, CancellationToken ct) =>
        await queries.FirstOrDefaultAsync(tasks.Query().Where(t => t.Id == rule.ScheduledTaskId && t.TenantId == rule.TenantId &&
            t.JobType == AiAnomalyContract.JobType && t.CronExpression == AiAnomalyContract.Cron && t.Queue == "default" && t.ParametersJson == null), ct)
            ?? throw new BusinessException(ErrorCode.ValidationFailed, "Invalid reminder task binding.");
    private Task<AiAnomalyEvent?> CurrentEvent(AiAnomalyRule rule, CancellationToken ct) => queries.FirstOrDefaultAsync(
        events.Query().Where(e => e.TenantId == rule.TenantId && e.RuleId == rule.Id && e.EpisodeSequence == rule.EpisodeSequence), ct);
    private async Task<AiAnomalyEvent> EventAsync(AiAnomalyRule rule, Guid id, CancellationToken ct) =>
        await queries.FirstOrDefaultAsync(events.Query().Where(e => e.Id == id && e.TenantId == rule.TenantId &&
            e.RuleId == rule.Id && e.RecipientUserId == rule.OwnerUserId), ct) ?? throw new BusinessException(ErrorCode.NotFound, "Reminder event was not found.");
    private static ControlledNotificationRequest Request(AiAnomalyRule rule, AiAnomalyEvent item) =>
        new(rule.TenantId, rule.OwnerUserId, item.DeliveryKey, AiAnomalyContract.SourceKind, item.Id,
            "待处理提醒", "有一条受控提醒可查看，请打开页面核对当前状态。", $"/system/ai-anomalies?eventId={item.Id:D}");
    private void ApplyReceipt(AiAnomalyRule rule, AiAnomalyEvent item, ControlledNotificationReceipt receipt)
    {
        item.ErrorCode = null; item.NextAttemptAt = null;
        item.NotificationId = receipt.NotificationId; item.MessageId = receipt.MessageId ?? item.MessageId;
        item.DeliveryStatus = receipt.Status switch { NotificationDeliveryStatuses.Delivered => AiAnomalyDeliveryStatus.Delivered,
            NotificationDeliveryStatuses.Queued => AiAnomalyDeliveryStatus.Queued, _ => AiAnomalyDeliveryStatus.Disabled };
        if (item.DeliveryStatus == AiAnomalyDeliveryStatus.Delivered)
        { rule.LastNotifiedAt = clock.GetUtcNow(); if (rule.ReservedEventId == item.Id) rule.ReservedEventId = null; }
        else if (item.DeliveryStatus == AiAnomalyDeliveryStatus.Queued) rule.ReservedEventId = item.Id;
    }
    private async Task Suppress(AiAnomalyRule rule, AiAnomalyEvent item, string code, CancellationToken ct)
    {
        item.Close(clock.GetUtcNow(), code);
        if (rule.ReservedEventId == item.Id) rule.ReservedEventId = null;
        events.Update(item); rules.Update(rule); await unit.SaveChangesAsync(ct);
    }
    private async Task ReconcileReservation(AiAnomalyRule rule, CancellationToken ct)
    {
        if (rule.ReservedEventId is not Guid id) return;
        var item = await EventAsync(rule, id, ct);
        if (item.DeliveryStatus != AiAnomalyDeliveryStatus.Queued) { rule.ReservedEventId = null; return; }
        var failed = await queries.AnyAsync(outbox.Query().Where(m => m.TenantId == rule.TenantId && m.MessageId == item.MessageId &&
            m.Status == ReliableMessageStatus.Failed), ct) || await queries.AnyAsync(deadLetters.Query().Where(m =>
                m.TenantId == rule.TenantId && m.MessageId == item.MessageId), ct);
        if (!failed) return;
        item.DeliveryStatus = AiAnomalyDeliveryStatus.Failed; item.ErrorCode = "transport_failed";
        item.NextAttemptAt = null; events.Update(item); rule.ReservedEventId = null;
    }
    private async Task Log(ScheduledTask task, string code, bool succeeded, CancellationToken ct)
    {
        var now = clock.GetUtcNow(); task.LastRunAt = now; task.LastRunSucceeded = succeeded; task.LastRunMessage = code;
        tasks.Update(task);
        await logs.AddAsync(new() { TenantId = task.TenantId, ScheduledTaskId = task.Id, JobType = task.JobType,
            StartedAt = now, FinishedAt = now, Succeeded = succeeded, Message = code, TraceId = trace.TraceId }, ct);
        await unit.SaveChangesAsync(ct);
    }
}
