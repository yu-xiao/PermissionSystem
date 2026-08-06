using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure;
using PermissionSystem.Infrastructure.HealthChecks;
using PermissionSystem.Shared.Results;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Notifications;

public sealed class NotificationDeliveryTests
{
    [Fact]
    public async Task DirectMode_ShouldPersistNotificationAndPushRealtimeMessage()
    {
        var fixture = CreateFixture(NotificationDeliveryMode.Direct);

        var result = await fixture.Service.SendSystemNotificationAsync(CreateRequest());

        Assert.Equal(NotificationDeliveryStatuses.Delivered, result.Status);
        Assert.Equal(NotificationDeliveryMode.Direct.ToString(), result.Mode);
        Assert.NotNull(result.NotificationId);
        Assert.Single(fixture.Notifications.Items);
        Assert.Single(fixture.UserNotifications.Items);
        Assert.Single(fixture.RealtimeSender.Messages);
        Assert.Empty(fixture.Outbox.Messages);
    }

    [Fact]
    public async Task OutboxRabbitMqMode_ShouldQueueWithoutPersistingNotification()
    {
        var fixture = CreateFixture(NotificationDeliveryMode.OutboxRabbitMQ);

        var result = await fixture.Service.SendSystemNotificationAsync(CreateRequest());

        Assert.Equal(NotificationDeliveryStatuses.Queued, result.Status);
        Assert.Equal(NotificationDeliveryMode.OutboxRabbitMQ.ToString(), result.Mode);
        Assert.Equal("test-message-1", result.MessageId);
        Assert.Single(fixture.Outbox.Messages);
        Assert.Empty(fixture.Notifications.Items);
        Assert.Empty(fixture.UserNotifications.Items);
        Assert.Empty(fixture.RealtimeSender.Messages);
    }

    [Fact]
    public async Task DisabledMode_ShouldReturnDisabledWithoutCreatingBacklog()
    {
        var fixture = CreateFixture(NotificationDeliveryMode.Disabled);

        var result = await fixture.Service.SendSystemNotificationAsync(CreateRequest());

        Assert.Equal(NotificationDeliveryStatuses.Disabled, result.Status);
        Assert.Equal(NotificationDeliveryMode.Disabled.ToString(), result.Mode);
        Assert.Empty(fixture.Outbox.Messages);
        Assert.Empty(fixture.Notifications.Items);
        Assert.Empty(fixture.UserNotifications.Items);
        Assert.Empty(fixture.RealtimeSender.Messages);
    }

    [Fact]
    public async Task DisabledModeHealthCheck_ShouldReportDegradedAndExposeMode()
    {
        var healthCheck = new NotificationDeliveryHealthCheck(new NotificationDeliveryOptions
        {
            DeliveryMode = NotificationDeliveryMode.Disabled
        });

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(NotificationDeliveryMode.Disabled.ToString(), result.Data["mode"]);
        Assert.Equal(false, result.Data["enabled"]);
    }

    [Fact]
    public void OutboxRabbitMqMode_ShouldRejectIncompleteRabbitMqConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=PermissionSystem;Trusted_Connection=True;",
                ["Notifications:DeliveryMode"] = NotificationDeliveryMode.OutboxRabbitMQ.ToString(),
                ["RabbitMQ:Enabled"] = "true",
                ["RabbitMQ:EnableConsumers"] = "false",
                ["RabbitMQ:EnableOutboxPublisher"] = "true"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(configuration));

        Assert.Contains("RabbitMQ:EnableConsumers", exception.Message, StringComparison.Ordinal);
    }

    private static SendSystemNotificationRequest CreateRequest()
    {
        return new SendSystemNotificationRequest
        {
            TenantId = TestIds.TenantId,
            RecipientUserIds = [TestIds.NormalUserId],
            Type = NotificationTypes.System,
            Title = "Delivery test",
            Content = "Notification delivery regression test."
        };
    }

    private static NotificationFixture CreateFixture(NotificationDeliveryMode mode)
    {
        var notifications = new InMemoryRepository<Notification>();
        var userNotifications = new InMemoryRepository<UserNotification>();
        var users = new InMemoryRepository<User>(new User
        {
            Id = TestIds.NormalUserId,
            TenantId = TestIds.TenantId,
            UserName = "notification-user",
            NormalizedUserName = "NOTIFICATION-USER",
            DisplayName = "Notification user",
            PasswordHash = "test",
            IsEnabled = true
        });
        var outbox = new RecordingOutboxService();
        var realtimeSender = new RecordingNotificationRealtimeSender();
        var currentUser = new TestCurrentUserService { TenantId = TestIds.TenantId };
        var service = new NotificationService(
            notifications,
            userNotifications,
            new InMemoryRepository<NotificationTemplate>(),
            users,
            currentUser,
            new TestTenantWriteResolver(),
            outbox,
            realtimeSender,
            new TestUnitOfWork(),
            new NotificationDeliveryOptions { DeliveryMode = mode });

        return new NotificationFixture(
            service,
            notifications,
            userNotifications,
            outbox,
            realtimeSender);
    }

    private sealed record NotificationFixture(
        NotificationService Service,
        InMemoryRepository<Notification> Notifications,
        InMemoryRepository<UserNotification> UserNotifications,
        RecordingOutboxService Outbox,
        RecordingNotificationRealtimeSender RealtimeSender);

    private sealed class RecordingNotificationRealtimeSender : INotificationRealtimeSender
    {
        public List<NotificationRealtimeMessage> Messages { get; } = [];

        public Task SendToUsersAsync(
            IReadOnlyCollection<Guid> userIds,
            NotificationRealtimeMessage message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxService : IOutboxService
    {
        public List<object> Messages { get; } = [];

        public Task<string> EnqueueAsync(
            CreateOutboxMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(request);
            return Task.FromResult($"test-message-{Messages.Count}");
        }

        public Task<string> EnqueueAsync<TMessage>(
            string exchange,
            string routingKey,
            TMessage message,
            IReadOnlyDictionary<string, string>? headers = null,
            Guid? tenantId = null,
            string? messageId = null,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message!);
            return Task.FromResult($"test-message-{Messages.Count}");
        }

        public Task<PagedResult<OutboxMessageResponse>> GetPagedAsync(
            OutboxMessageQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PagedResult<OutboxMessageResponse>.Create([], request.PageIndex, request.PageSize, 0));
        }

        public Task<OutboxMessageDetailResponse> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OutboxMessageDetailResponse());
        }
    }
}
