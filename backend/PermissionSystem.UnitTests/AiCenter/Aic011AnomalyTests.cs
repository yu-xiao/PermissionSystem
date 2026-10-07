using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiAnomalies;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic011AnomalyTests
{
    [Fact]
    public async Task Creation_IsPersonalFixedPausedAndDoesNotGrantPermissions()
    {
        var f = new Aic011TestFixture(); var r = await f.Create(false);
        Assert.False(r.IsEnabled); Assert.False(f.Tasks.Items.Single().IsEnabled); Assert.Equal(TestIds.NormalUserId, r.OwnerUserId);
        Assert.Null(f.Tasks.Items.Single().ParametersJson); Assert.Equal(0, f.BackgroundJobs.Registrations);
        Assert.False(new AiCenterOptions().EnableDemoAnomalyReminders);
        Assert.Equal(Aic011TestFixture.Permissions.Length + AiQueryTestFixture.Permissions.Length, f.Ai.Current.PermissionCodes.Count);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.CreateAsync());
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.TriggerAsync(r.Id));
    }

    [Theory]
    [InlineData(ApprovalStatus.Draft, 0)] [InlineData(ApprovalStatus.Pending, 1)] [InlineData(ApprovalStatus.Approved, 0)]
    [InlineData(ApprovalStatus.Rejected, 0)] [InlineData(ApprovalStatus.Withdrawn, 0)] [InlineData(ApprovalStatus.Cancelled, 0)]
    public async Task FixedRule_UsesCurrentPendingAndNoOtherStatus(ApprovalStatus status, int expected)
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); await f.Add(status);
        await f.Check(rule); Assert.Equal(expected, f.Events.Items.Count); Assert.Equal(expected, f.Notifications.Items.Count);
        Assert.Equal(0, f.Orders.UpdateCount); Assert.Equal("success", f.Logs.Items.Single().Succeeded ? "success" : "failed");
    }

    [Fact]
    public async Task FullCount_IsNotDisplayCountAndSensitiveFieldsNeverEnterNotificationOrTaskLog()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create();
        for (var i = 0; i < 250; i++) await f.Add();
        await f.Add(tenant: Guid.NewGuid()); (await f.Add()).IsDeleted = true;
        await f.Check(rule);
        Assert.Equal(250, Assert.Single(f.Events.Items).ObservedCount);
        var notification = Assert.Single(f.Notifications.Items);
        var json = JsonSerializer.Serialize(notification);
        Assert.DoesNotContain("sensitive-", json); Assert.DoesNotContain("250", notification.Content);
        Assert.Null(notification.Payload); Assert.DoesNotContain("250", Assert.Single(f.Logs.Items).Message);
        Assert.Equal(TestIds.NormalUserId, Assert.Single(f.UserNotifications.Items).UserId);
    }

    [Theory]
    [InlineData("all", 3)] [InlineData("self", 1)] [InlineData("department", 1)] [InlineData("union", 2)] [InlineData("empty", 0)]
    public async Task Scope_IsOriginalCreatedByAndDepartmentIntersection(string type, int count)
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); var department = Guid.NewGuid();
        await f.Add(creator: TestIds.NormalUserId); await f.Add(creator: Guid.NewGuid(), department: department);
        await f.Add(creator: Guid.NewGuid(), department: Guid.NewGuid());
        f.Ai.Scopes.Scope = new() { CurrentUserId = TestIds.NormalUserId,
            ScopeType = type == "all" ? DataScopeType.All : type == "self" ? DataScopeType.CurrentUser : DataScopeType.CustomDepartments,
            IncludeCurrentUser = type is "self" or "union", DepartmentIds = type is "department" or "union" ? [department] : [] };
        await f.Check(rule);
        if (count == 0) Assert.Empty(f.Events.Items); else Assert.Equal(count, Assert.Single(f.Events.Items).ObservedCount);
    }

    [Fact]
    public async Task SustainedAnomaly_IsNotRepeatedEvenAfter24HoursOrPauseResume()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); await f.Add();
        await f.Check(rule); f.Time.Advance(TimeSpan.FromDays(2));
        await f.Service.SetEnabledAsync(rule.Id, new() { IsEnabled = false, RowVersion = rule.RowVersion });
        await f.Check(rule);
        await f.Service.SetEnabledAsync(rule.Id, new() { IsEnabled = true, RowVersion = rule.RowVersion });
        await f.Check(rule); await f.Check(rule);
        Assert.Single(f.Notifications.Items); Assert.Single(f.Events.Items); Assert.Equal(1, f.Events.Items.Single().AttemptCount);
        Assert.Equal(TimeSpan.FromHours(8), f.BackgroundJobs.Zone!.BaseUtcOffset);
    }

    [Fact]
    public async Task RecoveryAndNewEpisode_Respect24HourBoundaryAndDoNotSendRecoveryMessage()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); var order = await f.Add();
        await f.Check(rule); order.ApprovalStatus = ApprovalStatus.Approved; await f.Check(rule);
        Assert.False(rule.IsAnomalous); Assert.Single(f.Notifications.Items);
        order.ApprovalStatus = ApprovalStatus.Pending; await f.Check(rule);
        Assert.Equal(2, f.Events.Items.Count); Assert.Equal(AiAnomalyDeliveryStatus.CoolingDown, f.Events.Items.Last().DeliveryStatus);
        f.Time.Advance(TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1)); await f.Check(rule); Assert.Single(f.Notifications.Items);
        f.Time.Advance(TimeSpan.FromSeconds(1)); await f.Check(rule); Assert.Equal(2, f.Notifications.Items.Count);
    }

    [Fact]
    public async Task ManualClosure_MutesUntilSuccessfulRecoveryAndPreservesCooldown()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); var order = await f.Add(); await f.Check(rule);
        var item = f.Events.Items.Single(); item.RowVersion = new byte[8];
        await f.Service.CloseAsync(item.Id, item.RowVersion); f.Time.Advance(TimeSpan.FromDays(2)); await f.Check(rule);
        Assert.Single(f.Notifications.Items); Assert.Single(f.Events.Items);
        order.ApprovalStatus = ApprovalStatus.Approved; await f.Check(rule);
        order.ApprovalStatus = ApprovalStatus.Pending; await f.Check(rule); Assert.Equal(2, f.Notifications.Items.Count);
    }

    [Theory]
    [InlineData("business")] [InlineData("update")] [InlineData("view")] [InlineData("notification")]
    [InlineData("disabled-user")] [InlineData("disabled-tenant")] [InlineData("disabled-ai")] [InlineData("disabled-feature")]
    [InlineData("unapproved")] [InlineData("wrong-tenant")]
    public async Task Execution_RejectsInactiveIdentityConfigurationAndRevokedPermissions(string type)
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); await f.Add();
        switch (type)
        {
            case "business": f.Revoke("demo-business-order:view"); break;
            case "update": f.Revoke(AiAnomalyContract.UpdatePermission); break;
            case "view": f.Revoke(AiAnomalyContract.ViewPermission); break;
            case "notification": f.Revoke(AiAnomalyContract.NotificationPermission); break;
            case "disabled-user": f.Ai.Identities.Active = false; break;
            case "disabled-tenant": f.Tenants.Active = false; break;
            case "disabled-ai": f.Config.Enabled = false; break;
            case "disabled-feature": f.Config.EnableDemoAnomalyReminders = false; break;
            case "unapproved": f.Config.AllowedTenantIds = []; break;
            case "wrong-tenant": f.Ai.Current.TenantId = Guid.NewGuid(); break;
        }
        await Assert.ThrowsAsync<BusinessException>(() => f.Check(rule)); Assert.Empty(f.Events.Items); Assert.Empty(f.Notifications.Items);
    }

    [Fact]
    public async Task RevokeAfterQuery_DoesNotRecordOrExposeObservedFacts()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); await f.Add();
        f.Query.AfterRead = () => f.Revoke("demo-business-order:view");
        await Assert.ThrowsAsync<BusinessException>(() => f.Check(rule)); Assert.Empty(f.Events.Items); Assert.Empty(f.Notifications.Items);
    }

    [Fact]
    public async Task QueryFailureOrCancellation_DoesNotCloseExistingEpisode()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); await f.Add(); await f.Check(rule);
        f.Query.Fail = true; await Assert.ThrowsAsync<TimeoutException>(() => f.Check(rule));
        Assert.True(rule.IsAnomalous); Assert.Null(f.Events.Items.Single().ClosedAt);
        f.Query.Fail = false; using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Execution.CheckAsync(rule.Id, cancel.Token));
        Assert.Null(f.Events.Items.Single().ClosedAt);
    }

    [Fact]
    public async Task FailedNotification_PreservesFactAndUsesBoundedDurableBackoff()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); await f.Add(); f.FailNotification = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Check(rule)); var item = f.Events.Items.Single();
        Assert.Equal(1, item.ObservedCount); Assert.Equal(1, item.AttemptCount); Assert.Null(rule.LastNotifiedAt);
        await f.Check(rule); Assert.Equal(1, item.AttemptCount);
        f.Time.Advance(TimeSpan.FromMinutes(5)); await Assert.ThrowsAsync<InvalidOperationException>(() => f.Check(rule));
        f.Time.Advance(TimeSpan.FromMinutes(15)); await Assert.ThrowsAsync<InvalidOperationException>(() => f.Check(rule));
        f.Time.Advance(TimeSpan.FromDays(3)); await f.Check(rule);
        Assert.Equal(3, item.AttemptCount); Assert.Equal(AiAnomalyDeliveryStatus.Failed, item.DeliveryStatus); Assert.Empty(f.Notifications.Items);
    }

    [Fact]
    public async Task DisabledDelivery_DoesNotConsumeSuccessfulCooldownAndCanResumeSameValidEpisode()
    {
        var f = new Aic011TestFixture(NotificationDeliveryMode.Disabled); var rule = await f.Create(false);
        rule.IsEnabled = f.Tasks.Items.Single().IsEnabled = true;
        await f.Add(); await f.Check(rule);
        Assert.Equal(AiAnomalyDeliveryStatus.Disabled, f.Events.Items.Single().DeliveryStatus);
        Assert.Equal(0, f.Events.Items.Single().AttemptCount); Assert.Null(rule.LastNotifiedAt); Assert.Empty(f.Notifications.Items);
    }

    [Fact]
    public async Task RealtimeFailure_DoesNotRetryDurableNotificationOrLoseSuccessReceipt()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); await f.Add(); f.Realtime.Fail = true;
        await f.Check(rule); await f.Check(rule);
        Assert.Single(f.Notifications.Items); Assert.Equal(AiAnomalyDeliveryStatus.Delivered, f.Events.Items.Single().DeliveryStatus);
        Assert.Equal(1, f.Realtime.Calls); Assert.NotNull(rule.LastNotifiedAt);
    }

    [Fact]
    public async Task Outbox_DeduplicatesPublicationAndConsumerEffectsWithDistinctStates()
    {
        var f = new Aic011TestFixture(NotificationDeliveryMode.OutboxRabbitMQ); var rule = await f.Create(); await f.Add();
        await f.Check(rule); await f.Check(rule); var item = f.Events.Items.Single(); var message = f.Outbox.Items.Single();
        Assert.Equal(item.DeliveryKey, message.MessageId); Assert.Empty(f.Notifications.Items);
        Assert.Equal(AiAnomalyDeliveryStatus.Queued, item.DeliveryStatus); Assert.Equal(item.Id, rule.ReservedEventId);
        Assert.Null(rule.LastNotifiedAt); Assert.DoesNotContain("sensitive-", message.Payload);
        var payload = JsonSerializer.Deserialize<NotificationCreatedEvent>(message.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        await f.NotificationService.HandleNotificationEventAsync(payload); await f.NotificationService.HandleNotificationEventAsync(payload);
        Assert.Single(f.Notifications.Items); Assert.Null(rule.ReservedEventId); Assert.NotNull(rule.LastNotifiedAt);
        Assert.Equal(AiAnomalyDeliveryStatus.Delivered, item.DeliveryStatus);
    }

    [Theory]
    [InlineData("pause")] [InlineData("close")] [InlineData("recover")] [InlineData("scope")]
    public async Task DelayedConsumption_RechecksStateAndSuppressesStaleNotification(string type)
    {
        var f = new Aic011TestFixture(NotificationDeliveryMode.OutboxRabbitMQ); var rule = await f.Create(); var order = await f.Add();
        await f.Check(rule); var item = f.Events.Items.Single();
        if (type == "pause") rule.IsEnabled = false;
        if (type == "close") item.Close(f.Time.Now, "manual");
        if (type == "recover") order.ApprovalStatus = ApprovalStatus.Approved;
        if (type == "scope") f.Ai.Scopes.Scope = new() { CurrentUserId = TestIds.NormalUserId, ScopeType = DataScopeType.CurrentUser };
        await f.Execution.DeliverQueuedAsync(rule.Id, item.Id, default);
        Assert.Empty(f.Notifications.Items); Assert.Equal(AiAnomalyDeliveryStatus.Suppressed, item.DeliveryStatus);
    }

    [Fact]
    public async Task RecipientRevocation_CannotDeliverAndSuppressionKeepsFact()
    {
        var f = new Aic011TestFixture(NotificationDeliveryMode.OutboxRabbitMQ); var rule = await f.Create(); await f.Add(); await f.Check(rule);
        var item = f.Events.Items.Single(); f.Revoke("demo-business-order:view");
        await Assert.ThrowsAsync<BusinessException>(() => f.Execution.DeliverQueuedAsync(rule.Id, item.Id, default));
        await f.Execution.SuppressQueuedAsync(rule.Id, item.Id, default);
        Assert.Equal(AiAnomalyDeliveryStatus.Suppressed, item.DeliveryStatus); Assert.Null(rule.ReservedEventId);
        Assert.Equal(1, item.ObservedCount); Assert.Empty(f.Notifications.Items);
    }

    [Theory]
    [InlineData("permission")] [InlineData("user")] [InlineData("tenant")] [InlineData("feature")]
    public async Task SourceHost_SuppressesQueuedEffectAfterAuthorizationIsLost(string reason)
    {
        var f = new Aic011TestFixture(NotificationDeliveryMode.OutboxRabbitMQ);
        var rule = await f.Create(); await f.Add(); await f.Check(rule);
        var item = Assert.Single(f.Events.Items);
        if (reason == "permission") f.Revoke("demo-business-order:view");
        if (reason == "user") f.Ai.Identities.Active = false;
        if (reason == "tenant") f.Tenants.Active = false;
        if (reason == "feature") f.Config.EnableDemoAnomalyReminders = false;
        await using var provider = HostServices(f);
        var host = new AiAnomalyExecutionHost(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AiAnomalyExecutionHost>.Instance);
        await host.HandleAsync(TestIds.TenantId, item.Id, default);
        Assert.Equal(AiAnomalyDeliveryStatus.Suppressed, item.DeliveryStatus);
        Assert.Null(rule.ReservedEventId); Assert.Null(rule.LastNotifiedAt);
        Assert.Equal(1, item.ObservedCount); Assert.Empty(f.Notifications.Items);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TerminalTransportFailure_ReleasesReservationWithoutRepublishing(bool deadLetter)
    {
        var f = new Aic011TestFixture(NotificationDeliveryMode.OutboxRabbitMQ);
        var rule = await f.Create(); await f.Add(); await f.Check(rule);
        var item = Assert.Single(f.Events.Items);
        if (deadLetter) await f.DeadLetters.AddAsync(new() { TenantId = rule.TenantId, MessageId = item.MessageId! });
        else f.Outbox.Items.Single().Status = ReliableMessageStatus.Failed;
        await f.Check(rule); f.Time.Advance(TimeSpan.FromDays(2)); await f.Check(rule);
        Assert.Equal(AiAnomalyDeliveryStatus.Failed, item.DeliveryStatus);
        Assert.Equal("transport_failed", item.ErrorCode); Assert.Null(rule.ReservedEventId);
        Assert.Null(rule.LastNotifiedAt); Assert.Equal(1, item.AttemptCount);
        Assert.Single(f.Outbox.Items); Assert.Empty(f.Notifications.Items);
    }

    [Fact]
    public async Task LockContention_RecordsSafeSkippedLogWithoutCheckingOrSending()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); await f.Add();
        var task = Assert.Single(f.Tasks.Items);
        await using var provider = HostServices(f);
        var host = new AiAnomalyExecutionHost(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AiAnomalyExecutionHost>.Instance);
        await new AiAnomalyScheduledJob(host, new ContendedLock()).ExecuteAsync(task.Id, default);
        Assert.Empty(f.Events.Items); Assert.Empty(f.Notifications.Items);
        var log = Assert.Single(f.Logs.Items);
        Assert.Equal("lock_held_skipped", log.Message); Assert.False(log.Succeeded);
        Assert.Equal(rule.TenantId, log.TenantId); Assert.Equal(task.Id, log.ScheduledTaskId);
    }

    [Fact]
    public async Task History_HidesOldCountOnScopeChangeAndRejectsOtherOwnersOrTenants()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); await f.Add(); await f.Check(rule); var item = f.Events.Items.Single();
        Assert.Equal(1, (await f.Service.EventAsync(item.Id)).ObservedCount);
        f.Ai.Scopes.Scope = new() { CurrentUserId = TestIds.NormalUserId, ScopeType = DataScopeType.CurrentUser };
        var hidden = await f.Service.EventAsync(item.Id); Assert.Null(hidden.ObservedCount); Assert.True(hidden.EvidenceUnavailable);
        item.RecipientUserId = Guid.NewGuid(); await Assert.ThrowsAsync<BusinessException>(() => f.Service.EventAsync(item.Id));
        item.RecipientUserId = TestIds.NormalUserId; item.TenantId = Guid.NewGuid();
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.EventAsync(item.Id));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EvidenceRead_ReauthorizesScopeAfterWaitingForFence(bool detail)
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); await f.Add(); await f.Check(rule);
        var item = Assert.Single(f.Events.Items); var holds = 0;
        f.BeforeFence = () =>
        {
            holds++;
            f.Ai.Scopes.Scope = new() { CurrentUserId = TestIds.NormalUserId, ScopeType = DataScopeType.CurrentUser };
        };
        var result = detail ? await f.Service.EventAsync(item.Id) : (await f.Service.EventsAsync(rule.Id, 1, 20)).Items.Single();
        Assert.Equal(1, holds); Assert.Null(result.ObservedCount); Assert.True(result.EvidenceUnavailable);
        Assert.Equal(item.ObservedAt, result.ObservedAt);
    }

    [Fact]
    public async Task Mutations_RequireRowVersionAndPauseRemainsAvailableWithFeatureOff()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create();
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SetEnabledAsync(rule.Id, new() { IsEnabled = false }));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SetEnabledAsync(rule.Id, new() { IsEnabled = false, RowVersion = Enumerable.Repeat((byte)1, 8).ToArray() }));
        f.Config.EnableDemoAnomalyReminders = false;
        await f.Service.SetEnabledAsync(rule.Id, new() { IsEnabled = false, RowVersion = rule.RowVersion }); Assert.False(rule.IsEnabled);
    }

    [Fact]
    public async Task GenericTaskEndpoints_CannotMutateTriggerDeleteOrCreateControlledTasks()
    {
        var f = new Aic011TestFixture(); var rule = await f.Create(); var task = f.Tasks.Items.Single();
        var generic = new ScheduledTaskService(f.Tasks, f.Logs, f.BackgroundJobs, new TestTenantWriteResolver(), new TestUnitOfWork(),
            new SystemScope(), f.Tenants, f.Config);
        foreach (var action in new Func<Task>[] { () => generic.EnableAsync(task.Id), () => generic.DisableAsync(task.Id),
            () => generic.TriggerAsync(task.Id), () => generic.DeleteAsync(task.Id),
            () => generic.UpdateAsync(task.Id, new() { Name = "fake", JobType = "DemoLog", CronExpression = "* * * * *", Queue = "default" }) })
            await Assert.ThrowsAsync<BusinessException>(action);
        await Assert.ThrowsAsync<BusinessException>(() => generic.CreateAsync(new() { Code = "fake", Name = "fake", JobType = AiAnomalyContract.JobType,
            CronExpression = AiAnomalyContract.Cron, Queue = "default", TenantId = TestIds.TenantId }));
        Assert.True(rule.IsEnabled); Assert.False(task.IsDeleted);
        await generic.SyncEnabledTasksAsync(); Assert.Equal(typeof(AiAnomalyScheduledJob), f.BackgroundJobs.LastJobType);
        await generic.SuspendTenantAsync(TestIds.TenantId); await generic.ResumeTenantAsync(TestIds.TenantId);
        Assert.True(f.BackgroundJobs.Removals > 0);
    }

    [Fact]
    public void BackgroundIdentity_CannotReplaceAnyHttpIdentity()
    {
        var identity = new AiRunExecutionIdentity(); var f = new Aic011TestFixture(); identity.Set(f.Ai.Identities.Actor, string.Empty);
        var http = new HttpContextAccessor(); var current = new CurrentUserService(http, identity);
        Assert.True(current.IsAuthenticated); Assert.Equal(TestIds.NormalUserId, current.UserId);
        http.HttpContext = new DefaultHttpContext(); Assert.False(current.IsAuthenticated); Assert.Null(current.UserId);
    }

    [Fact]
    public void SqlModel_ContainsTenantCompositeConstraintsPersistentNotificationKeyAndConcurrency()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=127.0.0.1,1;Database=ModelOnly;Integrated Security=True").Options,
            new PermissionSystem.Application.Tenants.TenantContext(), new NullAuditContext());
        var rule = db.Model.FindEntityType(typeof(AiAnomalyRule))!; var item = db.Model.FindEntityType(typeof(AiAnomalyEvent))!;
        Assert.Contains(rule.GetForeignKeys(), key => key.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "ScheduledTaskId"]));
        Assert.Contains(item.GetIndexes(), key => key.IsUnique && key.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "RuleId", "EpisodeSequence"]));
        Assert.True(rule.FindProperty("RowVersion")!.IsConcurrencyToken);
        var index = db.Model.FindEntityType(typeof(Notification))!.GetIndexes().Single(key => key.Properties.Last().Name == "DeliveryKey");
        Assert.True(index.IsUnique); Assert.Equal("[DeliveryKey] IS NOT NULL", index.GetFilter());
    }
    private static ServiceProvider HostServices(Aic011TestFixture f)
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<AiRunExecutionIdentity>();
        services.AddSingleton<IUserCredentialValidator>(f.Ai.Identities);
        services.AddSingleton<IAsyncQueryExecutor>(f.Ai.Queries);
        services.AddSingleton<IRepository<AiAnomalyRule>>(f.Rules);
        services.AddSingleton<IRepository<AiAnomalyEvent>>(f.Events);
        services.AddSingleton<IRepository<ScheduledTask>>(f.Tasks);
        services.AddSingleton<IRepository<ScheduledTaskExecutionLog>>(f.Logs);
        services.AddSingleton<IUnitOfWork, TestUnitOfWork>();
        services.AddSingleton<ISystemTenantScope, SystemScope>();
        services.AddScoped(p => new AiAnomalyExecutionService(f.Rules, f.Events, f.Tasks, f.Logs, f.Outbox, f.DeadLetters,
            new AiAnomalyAccessPolicy(p.GetRequiredService<AiRunExecutionIdentity>(), p.GetRequiredService<ITenantContext>(),
                f.Ai.Identities, f.Tenants, f.Config), f.Query, f.Ai.Queries, new TestUnitOfWork(), new TestFence(),
            f.NotificationService, f.NotificationService, f.Time, new TraceContextAccessor()));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    private sealed class TestFence : IAiAnomalyCommitFence { public Task HoldAsync(Guid ruleId, CancellationToken ct) => Task.CompletedTask; }
    private sealed class ContendedLock : IDistributedLock
    {
        public Task ExecuteWithLockAsync(string key, Func<CancellationToken, Task> action, TimeSpan? expiry = null,
            TimeSpan? waitTime = null, CancellationToken cancellationToken = default) => throw new TimeoutException();
        public Task<DistributedLockHandle?> TryAcquireAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DistributedLockHandle> AcquireAsync(string key, TimeSpan? expiry = null, TimeSpan? waitTime = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ReleaseAsync(DistributedLockHandle handle, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<T> ExecuteWithLockAsync<T>(string key, Func<CancellationToken, Task<T>> action, TimeSpan? expiry = null,
            TimeSpan? waitTime = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class SystemScope : ISystemTenantScope { public IDisposable Begin(string operation) => new Lease(); private sealed class Lease : IDisposable { public void Dispose() { } } }
}
