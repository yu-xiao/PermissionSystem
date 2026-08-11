using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Infrastructure.Messaging;

public sealed class NullMessageBus : IMessageBus
{
    private readonly ILogger<NullMessageBus> _logger;

    public NullMessageBus(ILogger<NullMessageBus> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled => false;

    public bool IsOutboxPublisherEnabled => false;

    public Task PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "RabbitMQ is disabled. Message {MessageType} was not published.",
            typeof(TMessage).FullName ?? typeof(TMessage).Name);

        return Task.CompletedTask;
    }

    public Task PublishAsync<TMessage>(
        string routingKey,
        TMessage message,
        string? exchangeName = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "RabbitMQ is disabled. Message {MessageType} was not published. Exchange: {Exchange}, RoutingKey: {RoutingKey}",
            typeof(TMessage).FullName ?? typeof(TMessage).Name,
            exchangeName,
            routingKey);

        return Task.CompletedTask;
    }

    public Task PublishAsync(
        string exchange,
        string routingKey,
        object message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "RabbitMQ is disabled. Message {MessageType} was not published. Exchange: {Exchange}, RoutingKey: {RoutingKey}",
            message.GetType().FullName,
            exchange,
            routingKey);

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<TMessage>(
        string queueName,
        string routingKey,
        Func<TMessage, CancellationToken, Task> handler,
        string? exchangeName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "RabbitMQ is disabled. Consumer for queue {QueueName} was not registered.",
            queueName);

        return Task.CompletedTask;
    }

    public Task PublishToQueueAsync<TMessage>(
        string queueName,
        TMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "RabbitMQ is disabled. Message {MessageType} was not published to queue {QueueName}.",
            typeof(TMessage).FullName ?? typeof(TMessage).Name,
            queueName);

        return Task.CompletedTask;
    }

    public Task PublishRawAsync(
        string exchange,
        string routingKey,
        string payload,
        string? messageType = null,
        string? headers = null,
        string? messageId = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "RabbitMQ is disabled. Raw message {MessageType} was not published. Exchange: {Exchange}, RoutingKey: {RoutingKey}",
            messageType,
            exchange,
            routingKey);

        return Task.CompletedTask;
    }
}
