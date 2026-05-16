using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace PermissionSystem.Infrastructure.HealthChecks;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConnectionFactory _connectionFactory;

    public RabbitMqHealthCheck(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            return channel.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ is available.")
                : HealthCheckResult.Unhealthy("RabbitMQ channel is closed.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is unavailable.", exception);
        }
    }
}
