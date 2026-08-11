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

    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly RabbitMqPublisherChannelPool _publisherChannelPool;
    private readonly RabbitMQOptions _options;
    private readonly ITraceContextAccessor _traceContextAccessor;
    private readonly ILogger<RabbitMqMessageBus> _logger;

    public RabbitMqMessageBus(
        RabbitMqConnectionManager connectionManager,
        RabbitMqPublisherChannelPool publisherChannelPool,
        IOptions<RabbitMQOptions> options,
        ITraceContextAccessor traceContextAccessor,
        ILogger<RabbitMqMessageBus> logger)
    {
        _connectionManager = connectionManager;
        _publisherChannelPool = publisherChannelPool;
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
        return PublishAsync(typeof(TMessage).Name, message, null, cancellationToken);
    }

    public async Task PublishAsync<TMessage>(
        string routingKey,
        TMessage message,
        string? exchangeName = null,
        CancellationToken cancellationToken = default)
    {
        var exchange = string.IsNullOrWhiteSpace(exchangeName) ? _options.ExchangeName : exchangeName;
        await PublishSerializedAsync(
            exchange,
            routingKey,
            Serialize(message),
            typeof(TMessage).FullName ?? typeof(TMessage).Name,
            null,
            Guid.NewGuid().ToString("N"),
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
            Guid.NewGuid().ToString("N"),
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

        var exchange = string.IsNullOrWhiteSpace(exchangeName) ? _options.ExchangeName : exchangeName;
        var topology = RabbitMqTopology.Create(queueName, exchange, routingKey);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var channel = await _connectionManager.CreateChannelAsync(channelOptions, cancellationToken);
        await RabbitMqTopology.DeclareAsync(channel, topology, _options, cancellationToken);
        await channel.BasicQosAsync(0, _options.PrefetchCount, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<TMessage>(
                    Encoding.UTF8.GetString(eventArgs.Body.ToArray()),
                    JsonOptions) ?? throw new JsonException("Message payload is empty.");

                await handler(message, cancellationToken);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "RabbitMQ message consume failed. Queue: {QueueName}", queueName);
                try
                {
                    await RabbitMqConsumerFailureHandler.RetryOrDeadLetterAsync(
                        channel,
                        eventArgs,
                        topology,
                        _options,
                        exception,
                        cancellationToken);
                }
                catch (Exception publishException)
                {
                    _logger.LogError(
                        publishException,
                        "RabbitMQ retry or dead-letter publishing failed. Queue: {QueueName}",
                        queueName);
                    await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
                }
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
        await using var lease = await _publisherChannelPool.RentAsync(cancellationToken);
        await lease.Channel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await PublishWithTraceAsync(
            lease.Channel,
            string.Empty,
            queueName,
            Serialize(message),
            typeof(TMessage).FullName ?? typeof(TMessage).Name,
            null,
            Guid.NewGuid().ToString("N"),
            null,
            cancellationToken);
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
        return PublishSerializedAsync(
            exchange,
            routingKey,
            Encoding.UTF8.GetBytes(payload),
            messageType,
            headers,
            messageId ?? Guid.NewGuid().ToString("N"),
            tenantId,
            cancellationToken);
    }

    private async Task PublishSerializedAsync(
        string exchange,
        string routingKey,
        ReadOnlyMemory<byte> body,
        string? messageType,
        string? headers,
        string messageId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var lease = await _publisherChannelPool.RentAsync(cancellationToken);
            await DeclareExchangeAsync(lease.Channel, exchange, cancellationToken);
            await PublishWithTraceAsync(
                lease.Channel,
                exchange,
                routingKey,
                body,
                messageType,
                headers,
                messageId,
                tenantId,
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

    private static Task DeclareExchangeAsync(IChannel channel, string exchange, CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(exchange)
            ? Task.CompletedTask
            : channel.ExchangeDeclareAsync(
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
        string messageId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("rabbitmq.publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", exchange);
        activity?.SetTag("messaging.rabbitmq.routing_key", routingKey);
        activity?.SetTag("messaging.message.id", messageId);

        var traceId = ResolveTraceId();
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
            Type = messageType,
            MessageId = messageId,
            Headers = BuildHeaders(headers, traceId, messageId, tenantId)
        };

        await channel.BasicPublishAsync(exchange, routingKey, mandatory: true, properties, body, cancellationToken);
    }

    private string ResolveTraceId()
    {
        return !string.IsNullOrWhiteSpace(_traceContextAccessor.TraceId)
            ? _traceContextAccessor.TraceId
            : Activity.Current?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString();
    }

    private static Dictionary<string, object?> BuildHeaders(
        string? headers,
        string traceId,
        string messageId,
        Guid? tenantId)
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
        values["X-Message-Id"] = messageId;
        if (tenantId.HasValue)
        {
            values["X-Tenant-Id"] = tenantId.Value.ToString("D");
        }

        if (Activity.Current is not null)
        {
            values["traceparent"] = Activity.Current.Id;
        }

        return values;
    }

    private static ReadOnlyMemory<byte> Serialize<TMessage>(TMessage message)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));
    }
}
