namespace PermissionSystem.Application.Abstractions;

public interface IMessageBus
{
    Task PublishAsync<TMessage>(
        string routingKey,
        TMessage message,
        string? exchangeName = null,
        CancellationToken cancellationToken = default);

    Task PublishToQueueAsync<TMessage>(
        string queueName,
        TMessage message,
        CancellationToken cancellationToken = default);
}
