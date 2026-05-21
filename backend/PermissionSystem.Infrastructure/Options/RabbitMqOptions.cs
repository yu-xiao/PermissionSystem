namespace PermissionSystem.Infrastructure.Options;

public sealed class RabbitMQOptions
{
    public const string SectionName = "RabbitMQ";

    public bool Enabled { get; init; }

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string UserName { get; init; } = "guest";

    public string Password { get; init; } = "guest";

    public string VirtualHost { get; init; } = "/";

    public string ExchangeName { get; init; } = "permission-system.exchange";

    public int RetryCount { get; init; } = 3;

    public int RetryIntervalSeconds { get; init; } = 5;

    public int ConnectionTimeoutSeconds { get; init; } = 10;

    public bool EnablePublisherConfirms { get; init; } = true;

    public bool EnableConsumers { get; init; }

    public bool EnableOutboxPublisher { get; init; }
}
