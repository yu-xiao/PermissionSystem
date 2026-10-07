using System.Linq.Expressions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiAnomalies;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Results;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

internal sealed class Aic011TestFixture
{
    public static readonly string[] Permissions = ["demo-business-order:view", "system:scheduled-task:view",
        "system:scheduled-task:create", "system:scheduled-task:update", "system:scheduled-task:trigger", "system:notification:view"];
    public AiQueryTestFixture Ai { get; } = new(Permissions);
    public InMemoryRepository<AiAnomalyRule> Rules { get; } = new();
    public InMemoryRepository<AiAnomalyEvent> Events { get; } = new();
    public InMemoryRepository<ScheduledTask> Tasks { get; } = new();
    public InMemoryRepository<ScheduledTaskExecutionLog> Logs { get; } = new();
    public InMemoryRepository<OutboxMessage> Outbox { get; } = new();
    public InMemoryRepository<InboxMessage> Inbox { get; } = new();
    public InMemoryRepository<DeadLetterMessage> DeadLetters { get; } = new();
    public InMemoryRepository<DemoBusinessOrder> Orders { get; } = new();
    public InMemoryRepository<Notification> Notifications { get; } = new();
    public InMemoryRepository<UserNotification> UserNotifications { get; } = new();
    public Configuration Config { get; } = new();
    public Clock Time { get; } = new();
    public TenantChecker Tenants { get; } = new();
    public Jobs BackgroundJobs { get; } = new();
    public Sender Realtime { get; } = new();
    public AiAnomalyAccessPolicy Access { get; }
    public NotificationService NotificationService { get; }
    public AiAnomalyService Service { get; }
    public AiAnomalyExecutionService Execution { get; }
    public QueryProbe Query { get; }
    public bool FailNotification { get; set; }
    public Action? BeforeFence { get; set; }

    public Aic011TestFixture(NotificationDeliveryMode mode = NotificationDeliveryMode.Direct)
    {
        Ai.Users.AddAsync(new User { Id = TestIds.NormalUserId, TenantId = TestIds.TenantId, IsEnabled = true }).GetAwaiter().GetResult();
        var source = new DemoBusinessOrderReadOnlyQueryService(new DataPermissionRepository<DemoBusinessOrder>(Orders,
            Ai.Scopes, new DataPermissionFilter(), new DemoBusinessOrderDataPermissionSpecification()), Ai.Departments,
            Ai.Current, Ai.Tenant, Ai.Identities, Ai.Scopes, new DataPermissionFilter(), new DemoBusinessOrderDataPermissionSpecification(), Ai.Queries);
        Query = new(source);
        Access = new(Ai.Current, Ai.Tenant, Ai.Identities, Tenants, Config);
        var unit = new TestUnitOfWork();
        var trace = new TraceContextAccessor();
        var outbox = new OutboxService(Outbox, Ai.Current, trace, Ai.Queries);
        var handler = new SourceHandler(this);
        NotificationService = new(Notifications, UserNotifications, new InMemoryRepository<NotificationTemplate>(),
            Ai.Users, Ai.Current, new TestTenantWriteResolver(), outbox, Realtime, unit,
            new() { DeliveryMode = mode }, new Lookup(this), [handler]);
        Execution = new(Rules, Events, Tasks, Logs, Outbox, DeadLetters, Access, Query, Ai.Queries,
            unit, new Fence(this), new NotificationProbe(this), NotificationService, Time, trace);
        Service = new(Rules, Events, Tasks, Outbox, Inbox, Access, Query, Ai.Queries, unit,
            new Fence(this), BackgroundJobs, NotificationService, Time);
    }
    public async Task<AiAnomalyRule> Create(bool enabled = true)
    {
        await Service.CreateAsync(); var rule = Rules.Items.Single();
        rule.RowVersion = new byte[8];
        if (enabled) await Service.SetEnabledAsync(rule.Id, new() { IsEnabled = true, RowVersion = rule.RowVersion });
        return rule;
    }
    public async Task<DemoBusinessOrder> Add(ApprovalStatus status = ApprovalStatus.Pending, Guid? creator = null, Guid? department = null, Guid? tenant = null)
    {
        var order = new DemoBusinessOrder { TenantId = tenant ?? TestIds.TenantId, Title = "sensitive-title", OrderNo = "sensitive-order",
            CustomerName = "sensitive-customer", OwnerUserId = Guid.NewGuid(), CreatedBy = creator ?? TestIds.NormalUserId,
            DepartmentId = department, ApprovalStatus = status };
        await Orders.AddAsync(order); return order;
    }
    public void Revoke(string permission) => Ai.Identities.Actor = Ai.Identities.Actor with
        { PermissionCodes = Ai.Identities.Actor.PermissionCodes.Where(p => p != permission).ToArray() };
    public Task Check(AiAnomalyRule rule) => Execution.CheckAsync(rule.Id, default);

    public sealed class Configuration : IAiCenterConfiguration
    {
        public bool Enabled { get; set; } = true;
        public bool EnableDemoAnomalyReminders { get; set; } = true;
        public IReadOnlyCollection<Guid> AllowedTenantIds { get; set; } = [TestIds.TenantId];
        public int ConversationRetentionDays => 30;
        public int AuditRetentionDays => 180;
    }
    public sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan delta) => Now += delta;
    }
    public sealed class TenantChecker : ITenantStatusChecker
    {
        public bool Active { get; set; } = true;
        public Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(Active);
    }
    public sealed class QueryProbe(IDemoBusinessOrderReadOnlyQueryService inner) : IDemoBusinessOrderReadOnlyQueryService
    {
        public Action? AfterRead { get; set; }
        public bool Fail { get; set; }
        public async Task<DemoBusinessOrderReadOnlyResult> QueryAsync(DemoBusinessOrderReadOnlyQuery query, CancellationToken cancellationToken = default)
        { if (Fail) throw new TimeoutException(); var result = await inner.QueryAsync(query, cancellationToken); AfterRead?.Invoke(); return result; }
        public Task<bool> CanReadAsync(DemoBusinessOrderReadOnlyQuery query, DemoBusinessOrderTableData data, CancellationToken cancellationToken = default) => inner.CanReadAsync(query, data, cancellationToken);
    }
    public sealed class Sender : INotificationRealtimeSender
    {
        public bool Fail { get; set; }
        public int Calls { get; private set; }
        public Task SendToUsersAsync(IReadOnlyCollection<Guid> userIds, NotificationRealtimeMessage message, CancellationToken cancellationToken = default)
        { Calls++; if (Fail) throw new InvalidOperationException("synthetic push failure"); return Task.CompletedTask; }
    }
    public sealed class Jobs : IBackgroundJobService
    {
        public bool IsEnabled { get; set; } = true;
        public int Registrations { get; private set; }
        public int Removals { get; private set; }
        public Type? LastJobType { get; private set; }
        public TimeZoneInfo? Zone { get; private set; }
        public string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall) { LastJobType = typeof(TJob); return "synthetic-job"; }
        public string Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay) => throw new NotSupportedException();
        public void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null, string queue = "default")
        { Assert.Equal(AiAnomalyContract.Cron, cronExpression); Registrations++; LastJobType = typeof(TJob); Zone = timeZone; }
        public void RemoveRecurring(string recurringJobId) => Removals++;
        public void TriggerRecurring(string recurringJobId) { }
        public bool Delete(string jobId) => false;
    }
    private sealed class Fence(Aic011TestFixture f) : IAiAnomalyCommitFence
    {
        public Task HoldAsync(Guid ruleId, CancellationToken ct) { f.BeforeFence?.Invoke(); return Task.CompletedTask; }
    }
    private sealed class Lookup(Aic011TestFixture f) : IControlledNotificationLookup
    {
        public Task<ControlledNotificationReceipt?> FindAsync(Guid tenantId, string deliveryKey, CancellationToken ct)
        {
            var notification = f.Notifications.Items.FirstOrDefault(n => n.TenantId == tenantId && n.DeliveryKey == deliveryKey);
            var message = f.Outbox.Items.FirstOrDefault(m => m.TenantId == tenantId && m.MessageId == deliveryKey);
            return Task.FromResult<ControlledNotificationReceipt?>(notification is not null ? new(NotificationDeliveryStatuses.Delivered, notification.Id) :
                message is not null ? new(NotificationDeliveryStatuses.Queued, MessageId: message.MessageId) : null);
        }
    }
    private sealed class SourceHandler(Aic011TestFixture f) : INotificationSourceHandler
    {
        public string SourceKind => AiAnomalyContract.SourceKind;
        public async Task HandleAsync(Guid tenantId, Guid sourceId, CancellationToken ct)
        {
            var item = f.Events.Items.Single(e => e.Id == sourceId && e.TenantId == tenantId);
            await f.Execution.DeliverQueuedAsync(item.RuleId, item.Id, ct);
        }
    }
    private sealed class NotificationProbe(Aic011TestFixture f) : IControlledNotificationService
    {
        public Task<ControlledNotificationReceipt> StageAsync(ControlledNotificationRequest request, CancellationToken ct)
        { if (f.FailNotification) throw new InvalidOperationException("synthetic send failure"); return f.NotificationService.StageAsync(request, ct); }
        public Task<ControlledNotificationReceipt> PersistAsync(ControlledNotificationRequest request, CancellationToken ct) => f.NotificationService.PersistAsync(request, ct);
        public Task PushAsync(string deliveryKey, CancellationToken ct) => f.NotificationService.PushAsync(deliveryKey, ct);
    }
}
