using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.Infrastructure.Messaging;

public static class RabbitMqConsumerFailureHandler
{
    public const string RetryCountHeader = "X-Consumer-Retry-Count";
    public const string FailureReasonHeader = "X-Dead-Letter-Reason";

    public static async Task RetryOrDeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        RabbitMqConsumerTopology topology,
        RabbitMQOptions options,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var retryCount = GetRetryCount(eventArgs.BasicProperties.Headers);
        var headers = CloneHeaders(eventArgs.BasicProperties.Headers);
        headers[RetryCountHeader] = retryCount + 1;
        headers["X-Original-Exchange"] = topology.ExchangeName;
        headers["X-Original-Routing-Key"] = topology.RoutingKey;
        headers["X-Source-Queue"] = topology.QueueName;

        if (retryCount < options.ConsumerRetryCount)
        {
            await PublishAsync(
                channel,
                topology.RetryExchangeName,
                topology.RetryRoutingKey,
                eventArgs,
                headers,
                cancellationToken);
        }
        else
        {
            headers[FailureReasonHeader] = Truncate(exception.Message, 2000);
            await PublishAsync(
                channel,
                topology.DeadLetterExchangeName,
                topology.DeadLetterRoutingKey,
                eventArgs,
                headers,
                cancellationToken);
        }

        await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
    }

    public static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        return GetRetryCount(properties.Headers);
    }

    private static async Task PublishAsync(
        IChannel channel,
        string exchange,
        string routingKey,
        BasicDeliverEventArgs eventArgs,
        Dictionary<string, object?> headers,
        CancellationToken cancellationToken)
    {
        var source = eventArgs.BasicProperties;
        var properties = new BasicProperties
        {
            ContentType = source.ContentType,
            ContentEncoding = source.ContentEncoding,
            DeliveryMode = source.DeliveryMode,
            Persistent = true,
            MessageId = source.MessageId,
            CorrelationId = source.CorrelationId,
            Type = source.Type,
            Timestamp = source.Timestamp,
            Headers = headers
        };

        await channel.BasicPublishAsync(
            exchange,
            routingKey,
            mandatory: true,
            properties,
            eventArgs.Body,
            cancellationToken);
    }

    private static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryCountHeader, out var value))
        {
            return 0;
        }

        return value switch
        {
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => Math.Max(0, parsed),
            string text when int.TryParse(text, out var parsed) => Math.Max(0, parsed),
            int parsed => Math.Max(0, parsed),
            long parsed when parsed <= int.MaxValue => Math.Max(0, (int)parsed),
            _ => 0
        };
    }

    private static Dictionary<string, object?> CloneHeaders(IDictionary<string, object?>? headers)
    {
        return headers is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(headers, StringComparer.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
