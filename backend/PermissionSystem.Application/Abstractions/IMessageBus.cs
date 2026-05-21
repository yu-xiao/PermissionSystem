namespace PermissionSystem.Application.Abstractions;

public interface IMessageBus
{
    bool IsEnabled { get; }

    bool IsOutboxPublisherEnabled { get; }

    Task PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default);

    Task PublishAsync<TMessage>(
        string routingKey,
        TMessage message,
        string? exchangeName = null,
        CancellationToken cancellationToken = default);

    Task PublishAsync(
        string exchange,
        string routingKey,
        object message,
        CancellationToken cancellationToken = default);

    Task SubscribeAsync<TMessage>(
        string queueName,
        string routingKey,
        Func<TMessage, CancellationToken, Task> handler,
        string? exchangeName = null,
        CancellationToken cancellationToken = default);

    Task PublishToQueueAsync<TMessage>(
        string queueName,
        TMessage message,
        CancellationToken cancellationToken = default);

    Task PublishRawAsync(
        string exchange,
        string routingKey,
        string payload,
        string? messageType = null,
        string? headers = null,
        CancellationToken cancellationToken = default);
}
