using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure.Options;
using RabbitMQ.Client;

namespace PermissionSystem.Infrastructure.Messaging;

public sealed class RabbitMqMessageBus : IMessageBus
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;

    public RabbitMqMessageBus(
        IConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> options)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
    }

    public async Task PublishAsync<TMessage>(
        string routingKey,
        TMessage message,
        string? exchangeName = null,
        CancellationToken cancellationToken = default)
    {
        var exchange = string.IsNullOrWhiteSpace(exchangeName)
            ? _options.DefaultExchange
            : exchangeName;

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(exchange))
        {
            await channel.ExchangeDeclareAsync(
                exchange,
                _options.DefaultExchangeType,
                durable: _options.Durable,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);
        }

        var body = Serialize(message);
        await channel.BasicPublishAsync(exchange, routingKey, body, cancellationToken);
    }

    public async Task PublishToQueueAsync<TMessage>(
        string queueName,
        TMessage message,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queueName,
            durable: _options.Durable,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = Serialize(message);
        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, body, cancellationToken);
    }

    private static ReadOnlyMemory<byte> Serialize<TMessage>(TMessage message)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }
}
