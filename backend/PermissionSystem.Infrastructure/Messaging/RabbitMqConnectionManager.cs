using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace PermissionSystem.Infrastructure.Messaging;

public sealed class RabbitMqConnectionManager : IAsyncDisposable
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqConnectionManager> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectionManager(
        IConnectionFactory connectionFactory,
        ILogger<RabbitMqConnectionManager> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            connection.ConnectionShutdownAsync += (_, eventArgs) =>
            {
                _logger.LogWarning(
                    "RabbitMQ connection was closed. ReplyCode: {ReplyCode}, ReplyText: {ReplyText}",
                    eventArgs.ReplyCode,
                    eventArgs.ReplyText);
                return Task.CompletedTask;
            };
            connection.RecoverySucceededAsync += (_, _) =>
            {
                _logger.LogInformation("RabbitMQ connection recovery completed.");
                return Task.CompletedTask;
            };
            connection.ConnectionRecoveryErrorAsync += (_, eventArgs) =>
            {
                _logger.LogWarning(eventArgs.Exception, "RabbitMQ connection recovery failed.");
                return Task.CompletedTask;
            };

            _connection = connection;
            _logger.LogInformation("RabbitMQ connection established.");
            return connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<IChannel> CreateChannelAsync(
        CreateChannelOptions? channelOptions = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        return channelOptions is null
            ? await connection.CreateChannelAsync(cancellationToken: cancellationToken)
            : await connection.CreateChannelAsync(channelOptions, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        finally
        {
            _connectionLock.Release();
            _connectionLock.Dispose();
        }
    }
}
