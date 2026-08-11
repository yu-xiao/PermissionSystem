using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PermissionSystem.Infrastructure.Options;
using RabbitMQ.Client;

namespace PermissionSystem.Infrastructure.Messaging;

public sealed class RabbitMqPublisherChannelPool : IAsyncDisposable
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly ConcurrentQueue<IChannel> _idleChannels = new();
    private readonly SemaphoreSlim _leases;
    private readonly bool _publisherConfirmsEnabled;

    public RabbitMqPublisherChannelPool(
        RabbitMqConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options)
    {
        _connectionManager = connectionManager;
        var configuredSize = Math.Max(1, options.Value.PublisherChannelPoolSize);
        _leases = new SemaphoreSlim(configuredSize, configuredSize);
        _publisherConfirmsEnabled = options.Value.EnablePublisherConfirms;
    }

    public async Task<RabbitMqPublisherChannelLease> RentAsync(CancellationToken cancellationToken = default)
    {
        await _leases.WaitAsync(cancellationToken);
        try
        {
            while (_idleChannels.TryDequeue(out var channel))
            {
                if (channel.IsOpen)
                {
                    return new RabbitMqPublisherChannelLease(this, channel);
                }

                await channel.DisposeAsync();
            }

            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: _publisherConfirmsEnabled,
                publisherConfirmationTrackingEnabled: _publisherConfirmsEnabled);
            var createdChannel = await _connectionManager.CreateChannelAsync(channelOptions, cancellationToken);
            return new RabbitMqPublisherChannelLease(this, createdChannel);
        }
        catch
        {
            _leases.Release();
            throw;
        }
    }

    private async ValueTask ReturnAsync(IChannel channel)
    {
        try
        {
            if (channel.IsOpen)
            {
                _idleChannels.Enqueue(channel);
            }
            else
            {
                await channel.DisposeAsync();
            }
        }
        finally
        {
            _leases.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        while (_idleChannels.TryDequeue(out var channel))
        {
            await channel.DisposeAsync();
        }

        _leases.Dispose();
    }

    public sealed class RabbitMqPublisherChannelLease : IAsyncDisposable
    {
        private RabbitMqPublisherChannelPool? _pool;

        internal RabbitMqPublisherChannelLease(RabbitMqPublisherChannelPool pool, IChannel channel)
        {
            _pool = pool;
            Channel = channel;
        }

        public IChannel Channel { get; }

        public async ValueTask DisposeAsync()
        {
            var pool = Interlocked.Exchange(ref _pool, null);
            if (pool is not null)
            {
                await pool.ReturnAsync(Channel);
            }
        }
    }
}
