using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Domain.Entities;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Messaging;

public sealed class OutboxTransactionTests
{
    [Fact]
    public async Task EnqueueAsync_ShouldAddPendingOutboxMessageWithTraceContext()
    {
        var messages = new InMemoryRepository<OutboxMessage>();
        var traceContext = new TraceContextAccessor { TraceId = "ea-023-trace" };
        var service = new OutboxService(
            messages,
            new TestCurrentUserService { TenantId = TestIds.TenantId },
            traceContext,
            new InMemoryAsyncQueryExecutor());

        var messageId = await service.EnqueueAsync(new CreateOutboxMessageRequest
        {
            Exchange = "notifications",
            RoutingKey = "notification.created",
            MessageType = "NotificationCreated",
            Payload = "{\"title\":\"EA-023\"}"
        });

        var message = Assert.Single(messages.Items);
        Assert.Equal(message.MessageId, messageId);
        Assert.Equal(TestIds.TenantId, message.TenantId);
        Assert.Equal("Pending", message.Status);
        Assert.Contains("ea-023-trace", message.Headers, StringComparison.Ordinal);
    }
}
