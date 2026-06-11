using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Infrastructure;
using PermissionSystem.Infrastructure.Caching;
using PermissionSystem.Infrastructure.Messaging;

namespace PermissionSystem.IntegrationTests.Configuration;

public sealed class ConfigurationSwitchIntegrationTests
{
    [Fact]
    public void RedisDisabled_ShouldUseMemoryCache()
    {
        using var provider = BuildProvider(("Cache:Provider", "Memory"), ("Cache:EnableRedis", "false"));

        var cache = provider.GetRequiredService<ICacheService>();

        Assert.IsType<MemoryCacheService>(cache);
    }

    [Fact]
    public void RabbitMqDisabled_ShouldUseNullMessageBus()
    {
        using var provider = BuildProvider(("RabbitMQ:Enabled", "false"));

        var messageBus = provider.GetRequiredService<IMessageBus>();

        Assert.IsType<NullMessageBus>(messageBus);
        Assert.False(messageBus.IsEnabled);
    }

    [Fact]
    public void RabbitMqDisabled_ShouldNotRegisterOutboxPublisherJob()
    {
        using var provider = BuildProvider(
            new[] { ("RabbitMQ:Enabled", "false") },
            registerOutboxPublisherJob: false);

        Assert.Null(provider.GetService<OutboxPublisherJob>());
    }

    [Fact]
    public void HangfireDisabledConfiguration_ShouldStillBuildServiceProvider()
    {
        using var provider = BuildProvider(("Hangfire:Enabled", "false"));

        Assert.NotNull(provider);
    }

    private static ServiceProvider BuildProvider(
        params (string Key, string Value)[] values)
    {
        return BuildProvider(values, registerOutboxPublisherJob: false);
    }

    private static ServiceProvider BuildProvider(
        IEnumerable<(string Key, string Value)> values,
        bool registerOutboxPublisherJob)
    {
        var data = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=PermissionSystem_Test;Trusted_Connection=True;TrustServerCertificate=True",
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["Cache:Provider"] = "Memory",
            ["Cache:EnableRedis"] = "false",
            ["RabbitMQ:Enabled"] = "false",
            ["Hangfire:Enabled"] = "false"
        };

        foreach (var (key, value) in values)
        {
            data[key] = value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(registerOutboxPublisherJob);
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}
