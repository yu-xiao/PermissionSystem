namespace PermissionSystem.Infrastructure.Options;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string UserName { get; init; } = "guest";

    public string Password { get; init; } = "guest";

    public string VirtualHost { get; init; } = "/";

    public string DefaultExchange { get; init; } = "permission-system";

    public string DefaultExchangeType { get; init; } = "topic";

    public bool Durable { get; init; } = true;

    public bool AutomaticRecoveryEnabled { get; init; } = true;
}
