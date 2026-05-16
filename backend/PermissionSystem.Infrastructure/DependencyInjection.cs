using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.BackgroundJobs;
using PermissionSystem.Infrastructure.Caching;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.HealthChecks;
using PermissionSystem.Infrastructure.Messaging;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Infrastructure.SeedData;
using RabbitMQ.Client;

namespace PermissionSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            options.UseOpenIddict();
        });

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IUserCredentialValidator, UserCredentialValidator>();
        services.AddScoped<SeedDataInitializer>();

        services.AddRedisInfrastructure(configuration);
        services.AddRabbitMqInfrastructure(configuration);
        services.AddHangfireInfrastructure(configuration, connectionString);

        return services;
    }

    public static IServiceCollection AddHangfireWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHangfireServer((serviceProvider, options) =>
        {
            var hangfireOptions = configuration
                .GetSection(HangfireOptions.SectionName)
                .Get<HangfireOptions>() ?? new HangfireOptions();

            options.WorkerCount = Math.Max(1, hangfireOptions.WorkerCount);
            options.Queues = hangfireOptions.Queues.Length > 0
                ? hangfireOptions.Queues
                : ["default"];
        });

        return services;
    }

    private static IServiceCollection AddRedisInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' is not configured.");
        var redisOptions = configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>() ?? new RedisOptions();

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = redisOptions.InstanceName;
        });

        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis");

        return services;
    }

    private static IServiceCollection AddRabbitMqInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddSingleton<IConnectionFactory>(serviceProvider =>
        {
            var options = configuration
                .GetSection(RabbitMqOptions.SectionName)
                .Get<RabbitMqOptions>() ?? new RabbitMqOptions();

            return new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost,
                AutomaticRecoveryEnabled = options.AutomaticRecoveryEnabled
            };
        });

        services.AddScoped<IMessageBus, RabbitMqMessageBus>();
        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>("rabbitmq");

        return services;
    }

    private static IServiceCollection AddHangfireInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string defaultConnectionString)
    {
        services.Configure<HangfireOptions>(configuration.GetSection(HangfireOptions.SectionName));

        var hangfireConnectionString = configuration.GetConnectionString("HangfireConnection")
            ?? defaultConnectionString;
        var hangfireOptions = configuration
            .GetSection(HangfireOptions.SectionName)
            .Get<HangfireOptions>() ?? new HangfireOptions();

        services.AddHangfire((serviceProvider, options) =>
        {
            options
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(
                    hangfireConnectionString,
                    new SqlServerStorageOptions
                    {
                        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                        QueuePollInterval = TimeSpan.FromSeconds(15),
                        UseRecommendedIsolationLevel = true,
                        DisableGlobalLocks = true,
                        SchemaName = hangfireOptions.SchemaName
                    });
        });

        services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
        services.AddHealthChecks().AddCheck(
            "hangfire",
            () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Hangfire storage is configured."));

        return services;
    }
}
