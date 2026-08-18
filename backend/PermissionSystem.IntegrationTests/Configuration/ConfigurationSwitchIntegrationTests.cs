using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Infrastructure.BackgroundJobs;
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

        var backgroundJobs = provider.GetRequiredService<IBackgroundJobService>();

        Assert.IsType<DisabledBackgroundJobService>(backgroundJobs);
        Assert.False(backgroundJobs.IsEnabled);
    }

    [Fact]
    public void HangfireDisabled_ShouldNotRegisterJobStorage()
    {
        var services = BuildServices(("Hangfire:Enabled", "false"));

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(JobStorage));
    }

    [Fact]
    public void HangfireWorkerDisabled_ShouldNotRegisterHangfireServer()
    {
        var (services, configuration) = BuildServicesWithConfiguration(
            [
                ("Hangfire:Enabled", "true"),
                ("Hangfire:WorkerEnabled", "false")
            ]);
        var registrationCount = services.Count;

        services.AddHangfireWorker(configuration);

        Assert.Equal(registrationCount, services.Count);
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
        var (services, _) = BuildServicesWithConfiguration(values, registerOutboxPublisherJob);
        return services.BuildServiceProvider();
    }

    private static ServiceCollection BuildServices(params (string Key, string Value)[] values)
    {
        return BuildServicesWithConfiguration(values, registerOutboxPublisherJob: false).Services;
    }

    private static (ServiceCollection Services, IConfiguration Configuration) BuildServicesWithConfiguration(
        IEnumerable<(string Key, string Value)> values,
        bool registerOutboxPublisherJob = false)
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

        return (services, configuration);
    }
}
