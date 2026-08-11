using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Results;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Messaging;

public sealed class EA024DeadLetterMessageTests
{
    [Fact]
    public async Task RecordAsync_ShouldUpsertFailureAndKeepLatestReason()
    {
        var messages = new InMemoryRepository<DeadLetterMessage>();
        var service = CreateService(messages, new RecordingMessageBus());

        await service.RecordAsync(CreateRequest("first failure"));
        await service.RecordAsync(CreateRequest("second failure"));

        var message = Assert.Single(messages.Items);
        Assert.Equal(DeadLetterMessageStatuses.Pending, message.Status);
        Assert.Equal("second failure", message.FailureReason);
        Assert.Equal(1, messages.UpdateCount);
    }

    [Fact]
    public async Task ReplayAsync_ShouldPublishOriginalMessageAndResetRetryHeaders()
    {
        var messages = new InMemoryRepository<DeadLetterMessage>();
        var bus = new RecordingMessageBus();
        var service = CreateService(messages, bus);
        await service.RecordAsync(CreateRequest("poison"));

        var id = Assert.Single(messages.Items).Id;
        await service.ReplayAsync(id);

        var message = messages.Items.Single();
        Assert.Equal(DeadLetterMessageStatuses.Replayed, message.Status);
        Assert.Equal(1, message.ReplayCount);
        Assert.Equal("permission-system.exchange", bus.Exchange);
        Assert.DoesNotContain("X-Consumer-Retry-Count", bus.Headers, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiscardAsync_ShouldRequireRemarkAndPreventReplay()
    {
        var messages = new InMemoryRepository<DeadLetterMessage>();
        var service = CreateService(messages, new RecordingMessageBus());
        await service.RecordAsync(CreateRequest("poison"));
        var id = Assert.Single(messages.Items).Id;

        await service.DiscardAsync(id, new DiscardDeadLetterMessageRequest { Remark = "已人工补录" });

        Assert.Equal(DeadLetterMessageStatuses.Discarded, messages.Items.Single().Status);
        await Assert.ThrowsAsync<PermissionSystem.Shared.Exceptions.BusinessException>(() => service.ReplayAsync(id));
    }

    private static DeadLetterMessageService CreateService(
        InMemoryRepository<DeadLetterMessage> messages,
        RecordingMessageBus bus)
    {
        return new DeadLetterMessageService(
            messages,
            new TestCurrentUserService { TenantId = TestIds.TenantId },
            new InMemoryAsyncQueryExecutor(),
            bus,
            new TestUnitOfWork());
    }

    private static RecordDeadLetterMessageRequest CreateRequest(string reason)
    {
        return new RecordDeadLetterMessageRequest
        {
            TenantId = TestIds.TenantId,
            MessageId = "ea024-message",
            Consumer = "permission-system.notifications",
            SourceQueue = "permission-system.notifications",
            Exchange = "permission-system.exchange",
            RoutingKey = "notifications.created",
            MessageType = "NotificationCreatedEvent",
            Payload = "{\"title\":\"test\"}",
            Headers = "{\"X-Consumer-Retry-Count\":\"3\",\"X-Tenant-Id\":\"10000000-0000-0000-0000-000000000001\"}",
            RetryCount = 3,
            FailureReason = reason
        };
    }

    private sealed class RecordingMessageBus : IMessageBus
    {
        public string Exchange { get; private set; } = string.Empty;

        public string Headers { get; private set; } = string.Empty;

        public bool IsEnabled => true;

        public bool IsOutboxPublisherEnabled => true;

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishAsync<TMessage>(string routingKey, TMessage message, string? exchangeName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishAsync(string exchange, string routingKey, object message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SubscribeAsync<TMessage>(string queueName, string routingKey, Func<TMessage, CancellationToken, Task> handler, string? exchangeName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishToQueueAsync<TMessage>(string queueName, TMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishRawAsync(string exchange, string routingKey, string payload, string? messageType = null, string? headers = null, string? messageId = null, Guid? tenantId = null, CancellationToken cancellationToken = default)
        {
            Exchange = exchange;
            Headers = headers ?? string.Empty;
            return Task.CompletedTask;
        }
    }
}
