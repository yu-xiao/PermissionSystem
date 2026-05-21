using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PermissionSystem.Infrastructure.Messaging;

public sealed class RabbitMqMessageBus : IMessageBus
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ActivitySource ActivitySource = new(TraceActivitySources.Messaging);

    private readonly IConnectionFactory _connectionFactory;
    private readonly RabbitMQOptions _options;
    private readonly ITraceContextAccessor _traceContextAccessor;
    private readonly ILogger<RabbitMqMessageBus> _logger;

    public RabbitMqMessageBus(
        IConnectionFactory connectionFactory,
        IOptions<RabbitMQOptions> options,
        ITraceContextAccessor traceContextAccessor,
        ILogger<RabbitMqMessageBus> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _traceContextAccessor = traceContextAccessor;
        _logger = logger;
    }

    public bool IsEnabled => true;

    public bool IsOutboxPublisherEnabled => _options.EnableOutboxPublisher;

    public Task PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
    {
        var routingKey = typeof(TMessage).Name;
        return PublishAsync(routingKey, message, null, cancellationToken);
    }

    public async Task PublishAsync<TMessage>(
        string routingKey,
        TMessage message,
        string? exchangeName = null,
        CancellationToken cancellationToken = default)
    {
        var exchange = string.IsNullOrWhiteSpace(exchangeName)
            ? _options.ExchangeName
            : exchangeName;

        await PublishSerializedAsync(
            exchange,
            routingKey,
            Serialize(message),
            typeof(TMessage).FullName ?? typeof(TMessage).Name,
            null,
            cancellationToken);
    }

    public Task PublishAsync(
        string exchange,
        string routingKey,
        object message,
        CancellationToken cancellationToken = default)
    {
        return PublishSerializedAsync(
            exchange,
            routingKey,
            Serialize(message),
            message.GetType().FullName ?? message.GetType().Name,
            null,
            cancellationToken);
    }

    public async Task SubscribeAsync<TMessage>(
        string queueName,
        string routingKey,
        Func<TMessage, CancellationToken, Task> handler,
        string? exchangeName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var exchange = string.IsNullOrWhiteSpace(exchangeName)
            ? _options.ExchangeName
            : exchangeName;

        await using var connection = await CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await DeclareExchangeAsync(channel, exchange, cancellationToken);
        await channel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queueName, exchange, routingKey, cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<TMessage>(
                    Encoding.UTF8.GetString(eventArgs.Body.ToArray()),
                    JsonOptions);
                if (message is null)
                {
                    throw new JsonException("Message payload is empty.");
                }

                await handler(message, cancellationToken);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "RabbitMQ message consume failed. Queue: {QueueName}", queueName);
                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            }
        };

        await channel.BasicConsumeAsync(queueName, autoAck: false, consumer, cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    public async Task PublishToQueueAsync<TMessage>(
        string queueName,
        TMessage message,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken);
        await using var channel = await CreatePublishChannelAsync(connection, cancellationToken);

        await channel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await PublishWithTraceAsync(
            channel,
            string.Empty,
            queueName,
            Serialize(message),
            typeof(TMessage).FullName ?? typeof(TMessage).Name,
            null,
            cancellationToken);
    }

    public Task PublishRawAsync(
        string exchange,
        string routingKey,
        string payload,
        string? messageType = null,
        string? headers = null,
        CancellationToken cancellationToken = default)
    {
        return PublishSerializedAsync(
            exchange,
            routingKey,
            Encoding.UTF8.GetBytes(payload),
            messageType,
            headers,
            cancellationToken);
    }

    private async Task PublishSerializedAsync(
        string exchange,
        string routingKey,
        ReadOnlyMemory<byte> body,
        string? messageType,
        string? headers,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await CreateConnectionAsync(cancellationToken);
            await using var channel = await CreatePublishChannelAsync(connection, cancellationToken);
            await DeclareExchangeAsync(channel, exchange, cancellationToken);

            await PublishWithTraceAsync(
                channel,
                exchange,
                routingKey,
                body,
                messageType,
                headers,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "RabbitMQ publish failed. Exchange: {Exchange}, RoutingKey: {RoutingKey}, MessageType: {MessageType}",
                exchange,
                routingKey,
                messageType);
            throw;
        }
    }

    private Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        return _connectionFactory.CreateConnectionAsync(cancellationToken);
    }

    private Task<IChannel> CreatePublishChannelAsync(
        IConnection connection,
        CancellationToken cancellationToken)
    {
        if (!_options.EnablePublisherConfirms)
        {
            return connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        return connection.CreateChannelAsync(channelOptions, cancellationToken);
    }

    private static Task DeclareExchangeAsync(
        IChannel channel,
        string exchange,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            return Task.CompletedTask;
        }

        return channel.ExchangeDeclareAsync(
            exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private async Task PublishWithTraceAsync(
        IChannel channel,
        string exchange,
        string routingKey,
        ReadOnlyMemory<byte> body,
        string? messageType,
        string? headers,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("rabbitmq.publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", exchange);
        activity?.SetTag("messaging.rabbitmq.routing_key", routingKey);

        var traceId = ResolveTraceId();
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
            Type = messageType,
            Headers = BuildHeaders(headers, traceId)
        };

        await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, properties, body, cancellationToken);
    }

    private string ResolveTraceId()
    {
        if (!string.IsNullOrWhiteSpace(_traceContextAccessor.TraceId))
        {
            return _traceContextAccessor.TraceId;
        }

        return Activity.Current?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString();
    }

    private static Dictionary<string, object?> BuildHeaders(string? headers, string traceId)
    {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(headers))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(headers, JsonOptions);
                if (parsed is not null)
                {
                    foreach (var (key, value) in parsed)
                    {
                        values[key] = value;
                    }
                }
            }
            catch (JsonException)
            {
                values["raw-headers"] = headers;
            }
        }

        values["X-Trace-Id"] = traceId;
        if (Activity.Current is not null)
        {
            values["traceparent"] = Activity.Current.Id;
        }

        return values;
    }

    private static ReadOnlyMemory<byte> Serialize<TMessage>(TMessage message)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }
}
