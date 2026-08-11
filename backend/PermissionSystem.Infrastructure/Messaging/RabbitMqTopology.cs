using PermissionSystem.Infrastructure.Options;
using RabbitMQ.Client;

namespace PermissionSystem.Infrastructure.Messaging;

public sealed record RabbitMqConsumerTopology(
    string QueueName,
    string ExchangeName,
    string RoutingKey,
    string RetryExchangeName,
    string RetryQueueName,
    string RetryRoutingKey,
    string DeadLetterExchangeName,
    string DeadLetterQueueName,
    string DeadLetterRoutingKey);

public static class RabbitMqTopology
{
    public static RabbitMqConsumerTopology Create(string queueName, string exchangeName, string routingKey)
    {
        return new RabbitMqConsumerTopology(
            queueName,
            exchangeName,
            routingKey,
            $"{exchangeName}.retry",
            $"{queueName}.retry",
            $"{routingKey}.retry",
            $"{exchangeName}.dlx",
            $"{queueName}.dlq",
            $"{routingKey}.dlq");
    }

    public static async Task DeclareAsync(
        IChannel channel,
        RabbitMqConsumerTopology topology,
        RabbitMQOptions options,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            topology.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            topology.RetryExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            topology.DeadLetterExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            topology.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            topology.QueueName,
            topology.ExchangeName,
            topology.RoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            topology.RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = Math.Max(1, options.ConsumerRetryDelaySeconds) * 1000,
                ["x-dead-letter-exchange"] = topology.ExchangeName,
                ["x-dead-letter-routing-key"] = topology.RoutingKey
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            topology.RetryQueueName,
            topology.RetryExchangeName,
            topology.RetryRoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            topology.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            topology.DeadLetterQueueName,
            topology.DeadLetterExchangeName,
            topology.DeadLetterRoutingKey,
            cancellationToken: cancellationToken);
    }
}
