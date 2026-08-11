using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Infrastructure.Messaging;
using PermissionSystem.Infrastructure.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PermissionSystem.Api.Services;

public sealed class NotificationEventConsumerHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ConsumerName = NotificationMessageNames.QueueName;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly RabbitMQOptions _options;
    private readonly ILogger<NotificationEventConsumerHostedService> _logger;

    public NotificationEventConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        ILogger<NotificationEventConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Notification event consumer stopped unexpectedly. It will retry soon.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var topology = RabbitMqTopology.Create(
            NotificationMessageNames.QueueName,
            NotificationMessageNames.Exchange,
            NotificationMessageNames.RoutingKey);
        var consumerChannelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var channel = await _connectionManager.CreateChannelAsync(consumerChannelOptions, stoppingToken);
        await using var deadLetterChannel = await _connectionManager.CreateChannelAsync(cancellationToken: stoppingToken);
        await RabbitMqTopology.DeclareAsync(channel, topology, _options, stoppingToken);
        await RabbitMqTopology.DeclareAsync(deadLetterChannel, topology, _options, stoppingToken);
        await channel.BasicQosAsync(0, _options.PrefetchCount, global: false, stoppingToken);
        await deadLetterChannel.BasicQosAsync(0, _options.PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            await HandleMessageAsync(channel, topology, eventArgs, stoppingToken);
        };
        await channel.BasicConsumeAsync(
            topology.QueueName,
            autoAck: false,
            consumer,
            cancellationToken: stoppingToken);

        var deadLetterConsumer = new AsyncEventingBasicConsumer(deadLetterChannel);
        deadLetterConsumer.ReceivedAsync += async (_, eventArgs) =>
        {
            await HandleDeadLetterAsync(deadLetterChannel, topology, eventArgs, stoppingToken);
        };
        await deadLetterChannel.BasicConsumeAsync(
            topology.DeadLetterQueueName,
            autoAck: false,
            deadLetterConsumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        RabbitMqConsumerTopology topology,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        try
        {
            var notificationEvent = JsonSerializer.Deserialize<NotificationCreatedEvent>(body, JsonOptions)
                ?? throw new JsonException("Notification event payload is empty.");
            var messageId = ResolveMessageId(eventArgs);
            if (string.IsNullOrWhiteSpace(messageId))
            {
                throw new JsonException("Notification event MessageId is required.");
            }

            using var scope = _scopeFactory.CreateScope();
            var traceContextAccessor = scope.ServiceProvider.GetRequiredService<ITraceContextAccessor>();
            traceContextAccessor.TraceId = ResolveHeader(eventArgs, "X-Trace-Id") ?? string.Empty;
            var tenantId = notificationEvent.TenantId ?? ResolveGuidHeader(eventArgs, "X-Tenant-Id");
            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                throw new JsonException("Notification event TenantId is required.");
            }

            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.SetTenant(tenantId.Value, "Message");
            var inboxService = scope.ServiceProvider.GetRequiredService<IInboxService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var tenantStatusChecker = scope.ServiceProvider.GetRequiredService<ITenantStatusChecker>();
            var payload = body;

            await inboxService.ExecuteOnceAsync(
                new InboxConsumeRequest
                {
                    TenantId = tenantId,
                    MessageId = messageId,
                    Consumer = ConsumerName,
                    MessageType = eventArgs.BasicProperties.Type ?? nameof(NotificationCreatedEvent),
                    Payload = payload
                },
                async token =>
                {
                    if (!await tenantStatusChecker.IsActiveAsync(tenantId.Value, token))
                    {
                        _logger.LogInformation(
                            "Notification event skipped because tenant is not active. TenantId: {TenantId}",
                            tenantId.Value);
                        return;
                    }

                    await notificationService.HandleNotificationEventAsync(notificationEvent, token);
                },
                cancellationToken);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to consume notification event.");
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
                _logger.LogError(publishException, "Failed to route notification event failure. Message remains queued.");
                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
            }
        }
    }

    private async Task HandleDeadLetterAsync(
        IChannel channel,
        RabbitMqConsumerTopology topology,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = ResolveGuidHeader(eventArgs, "X-Tenant-Id");
            var messageId = ResolveMessageId(eventArgs);
            if (!tenantId.HasValue || tenantId.Value == Guid.Empty || string.IsNullOrWhiteSpace(messageId))
            {
                throw new JsonException("Dead-letter message must contain X-Tenant-Id and X-Message-Id headers.");
            }

            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId.Value, "DeadLetter");
            var deadLetterService = scope.ServiceProvider.GetRequiredService<IDeadLetterMessageService>();
            var payload = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
            await deadLetterService.RecordAsync(
                new RecordDeadLetterMessageRequest
                {
                    TenantId = tenantId.Value,
                    MessageId = messageId,
                    Consumer = ConsumerName,
                    SourceQueue = ResolveHeader(eventArgs, "X-Source-Queue") ?? topology.QueueName,
                    Exchange = ResolveHeader(eventArgs, "X-Original-Exchange") ?? topology.ExchangeName,
                    RoutingKey = ResolveHeader(eventArgs, "X-Original-Routing-Key") ?? topology.RoutingKey,
                    MessageType = eventArgs.BasicProperties.Type ?? nameof(NotificationCreatedEvent),
                    Payload = payload,
                    Headers = SerializeHeaders(eventArgs.BasicProperties.Headers),
                    RetryCount = RabbitMqConsumerFailureHandler.GetRetryCount(eventArgs.BasicProperties),
                    FailureReason = ResolveHeader(eventArgs, RabbitMqConsumerFailureHandler.FailureReasonHeader)
                        ?? "RabbitMQ consumer rejected the message."
                },
                cancellationToken);

            _logger.LogError(
                "RabbitMQ dead-letter message persisted. MessageId: {MessageId}, TenantId: {TenantId}, FailureReason: {FailureReason}",
                messageId,
                tenantId.Value,
                ResolveHeader(eventArgs, RabbitMqConsumerFailureHandler.FailureReasonHeader) ?? "RabbitMQ consumer rejected the message.");

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to persist a RabbitMQ dead-letter message. Message remains in DLQ.");
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }

    private static string? ResolveMessageId(BasicDeliverEventArgs eventArgs)
    {
        return ResolveHeader(eventArgs, "X-Message-Id") ?? eventArgs.BasicProperties.MessageId;
    }

    private static Guid? ResolveGuidHeader(BasicDeliverEventArgs eventArgs, string name)
    {
        var value = ResolveHeader(eventArgs, name);
        return Guid.TryParse(value, out var tenantId) ? tenantId : null;
    }

    private static string? ResolveHeader(BasicDeliverEventArgs eventArgs, string name)
    {
        if (eventArgs.BasicProperties.Headers is null ||
            !eventArgs.BasicProperties.Headers.TryGetValue(name, out var value))
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => value?.ToString()
        };
    }

    private static string? SerializeHeaders(IDictionary<string, object?>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return null;
        }

        var values = headers.ToDictionary(
            pair => pair.Key,
            pair => pair.Value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                null => string.Empty,
                _ => pair.Value.ToString() ?? string.Empty
            },
            StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(values, JsonOptions);
    }
}
