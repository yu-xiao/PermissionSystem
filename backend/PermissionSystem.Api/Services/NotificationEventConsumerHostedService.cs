using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Infrastructure.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PermissionSystem.Api.Services;

public sealed class NotificationEventConsumerHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly RabbitMQOptions _options;
    private readonly ILogger<NotificationEventConsumerHostedService> _logger;

    public NotificationEventConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        IOptions<RabbitMQOptions> options,
        ILogger<NotificationEventConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
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
        await using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            NotificationMessageNames.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            NotificationMessageNames.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(
            NotificationMessageNames.QueueName,
            NotificationMessageNames.Exchange,
            NotificationMessageNames.RoutingKey,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            await HandleMessageAsync(channel, eventArgs, stoppingToken);
        };

        await channel.BasicConsumeAsync(
            NotificationMessageNames.QueueName,
            autoAck: false,
            consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
            var notificationEvent = JsonSerializer.Deserialize<NotificationCreatedEvent>(body, JsonOptions)
                ?? throw new JsonException("Notification event payload is empty.");

            using var scope = _scopeFactory.CreateScope();
            var traceContextAccessor = scope.ServiceProvider.GetRequiredService<ITraceContextAccessor>();
            traceContextAccessor.TraceId = ResolveTraceId(eventArgs) ?? string.Empty;
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            if (!notificationEvent.TenantId.HasValue || notificationEvent.TenantId.Value == Guid.Empty)
            {
                throw new JsonException("Notification event TenantId is required.");
            }

            tenantContext.SetTenant(notificationEvent.TenantId.Value, "Message");

            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notificationService.HandleNotificationEventAsync(notificationEvent, cancellationToken);
            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to consume notification event.");
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
    }

    private static string? ResolveTraceId(BasicDeliverEventArgs eventArgs)
    {
        if (eventArgs.BasicProperties.Headers is null)
        {
            return null;
        }

        if (!eventArgs.BasicProperties.Headers.TryGetValue("X-Trace-Id", out var value))
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
}
