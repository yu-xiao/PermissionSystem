using System.Reflection;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minio;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Files;
using PermissionSystem.Application.Integration;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.Reports;
using PermissionSystem.Application.Security;
using PermissionSystem.Application.Sso;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Authentication;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.BackgroundJobs;
using PermissionSystem.Infrastructure.Caching;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Files;
using PermissionSystem.Infrastructure.HealthChecks;
using PermissionSystem.Infrastructure.Idempotency;
using PermissionSystem.Infrastructure.Integration;
using PermissionSystem.Infrastructure.Locks;
using PermissionSystem.Infrastructure.Messaging;
using PermissionSystem.Infrastructure.Mcp;
using PermissionSystem.Infrastructure.Observability;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Infrastructure.RateLimiting;
using PermissionSystem.Infrastructure.Reports;
using PermissionSystem.Infrastructure.Repositories;
using PermissionSystem.Infrastructure.Queries;
using PermissionSystem.Infrastructure.Security;
using PermissionSystem.Infrastructure.SeedData;
using PermissionSystem.Infrastructure.Sso;
using PermissionSystem.Infrastructure.Tokens;
using PermissionSystem.Infrastructure.Tenancy;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace PermissionSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string? environmentName = null,
        params Assembly[] moduleAssemblies)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.Configure<OpenTelemetryOptions>(configuration.GetSection(OpenTelemetryOptions.SectionName));
        services.AddOptions<AiCenterOptions>()
            .Bind(configuration.GetSection(AiCenterOptions.SectionName))
            .Validate(options =>
                    !options.Enabled ||
                    (options.AllowedTenantIds is { Length: > 0 } &&
                     options.AllowedTenantIds.All(id => id != Guid.Empty)),
                "AI enabled configuration requires explicit non-empty tenant identifiers.")
            .Validate(options =>
                    options.ConversationRetentionDays is >= 1 and <= 365 &&
                    options.AuditRetentionDays is >= 30 and <= 3650 &&
                    options.AuditRetentionDays >= options.ConversationRetentionDays,
                "AI retention configuration is invalid.")
            .Validate(options => options.MaxToolRows is >= 1 and <= 200,
                "AI MaxToolRows must be between 1 and 200.")
            .Validate(options =>
                    options.DraftExpirationMinutes is >= 5 and <= 1440 &&
                    options.ConfirmationExpirationMinutes is >= 1 and <= 10 &&
                    options.DraftRetentionDays >= 1 &&
                    options.DraftRetentionDays <= options.AuditRetentionDays,
                "AI draft expiration or retention configuration is invalid.")
            .Validate(options =>
                    !options.EnableReportDatasetTool ||
                    (options.ApprovedReportDatasetKeys is { Length: > 0 } &&
                     options.ApprovedReportDatasetKeys.All(key => !string.IsNullOrWhiteSpace(key))),
                "AI report dataset tool requires at least one approved dataset key.")
            .Validate(options => options.RunWatchdogIntervalSeconds is >= 5 and <= 300 &&
                    options.RunOrphanTimeoutSeconds >= options.RunWatchdogIntervalSeconds &&
                    options.RunOrphanTimeoutSeconds <= 3600,
                "AI Run watchdog configuration is invalid.")
            .Validate(options => options.RequestLimitPerMinute is >= 1 and <= 10000 &&
                    options.ConcurrentRunLimit is >= 1 and <= 1000 &&
                    options.TokenLimitPerHour is >= 1000 and <= 100000000,
                "AI quota configuration is invalid.")
            .ValidateOnStart();
        services.AddSingleton<IAiCenterConfiguration>(serviceProvider =>
            serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiCenterOptions>>().Value);
        services.AddSingleton<IAiToolConfiguration>(serviceProvider =>
            serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiCenterOptions>>().Value);
        services.AddSingleton<IAiDraftConfiguration>(serviceProvider =>
            serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiCenterOptions>>().Value);
        services.Configure<OpenAiCompatibleOptions>(configuration.GetSection(OpenAiCompatibleOptions.SectionName));
        services.AddHttpClient<IAiModelClient, OpenAiCompatibleModelClient>(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddHttpClient("AiProviderConnectionTest", client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddScoped<IAiProviderConnectionTester, AiProviderConnectionTester>();
        services.AddHttpClient("AiModelGateway", client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddScoped<IAiModelGateway, OpenAiCompatibleModelGateway>();
        services.AddScoped<IAiCircuitBreaker, AiCircuitBreaker>();
        services.AddScoped<IAiRunCancellationProbe, AiRunCancellationProbe>();
        services.AddHostedService<AiRetentionHostedService>();
        services.Configure<LogArchiveOptions>(configuration.GetSection(LogArchiveOptions.SectionName));
        services.AddSingleton<DbCommandMetricsInterceptor>();
        services.AddSingleton<LogArchiveService>();
        services.AddHostedService<LogArchiveHostedService>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<DbCommandMetricsInterceptor>());
            options.UseOpenIddict();
        });

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IAsyncQueryExecutor, EfCoreAsyncQueryExecutor>();
        services.AddScoped<ITenantDirectoryRepository, TenantDirectoryRepository>();
        services.AddScoped<ITenantStatusChecker, TenantStatusChecker>();
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddHttpClient("Webhook", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IUserCredentialValidator, UserCredentialValidator>();
        services.AddScoped<IStepUpVerificationStore, StepUpVerificationStore>();
        services.AddScoped<IAiDocumentExecutionRecoveryStore, AiDocumentExecutionRecoveryStore>();
        services.AddScoped<IMcpClientBindingStore, McpClientBindingStore>();
        services.AddScoped<IMcpOAuthClientProvisioner, McpOAuthClientProvisioner>();
        services.AddScoped<IUserSessionStatusChecker, UserSessionStatusChecker>();
        services.AddScoped<ITokenRevocationService, OpenIddictTokenRevocationService>();
        services.AddScoped<IOidcClientService, OidcClientService>();
        services.AddSingleton<IConfigValueProtector, AesConfigValueProtector>();
        services.AddScoped<SeedDataInitializer>();
        var fileStorageOptions = configuration
            .GetSection(FileStorageOptions.SectionName)
            .Get<FileStorageOptions>() ?? new FileStorageOptions();
        FileStorageConfigurationValidator.Validate(fileStorageOptions, environmentName);
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton(fileStorageOptions);
        services.Configure<LockOptions>(configuration.GetSection(LockOptions.SectionName));
        services.AddSingleton(configuration.GetSection(LockOptions.SectionName).Get<LockOptions>() ?? new LockOptions());
        var reportOptions = configuration.GetSection(ReportOptions.SectionName).Get<ReportOptions>() ?? new ReportOptions();
        ValidateReportOptions(reportOptions, connectionString);
        services.Configure<ReportOptions>(configuration.GetSection(ReportOptions.SectionName));
        services.Configure<SsoOptions>(configuration.GetSection(SsoOptions.SectionName));
        services.AddSingleton<ISsoConfiguration>(serviceProvider =>
            serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SsoOptions>>().Value);
        var notificationDeliveryOptions = configuration
            .GetSection(NotificationDeliveryOptions.SectionName)
            .Get<NotificationDeliveryOptions>() ?? new NotificationDeliveryOptions();
        ValidateNotificationDeliveryOptions(notificationDeliveryOptions, configuration);
        services.AddSingleton(notificationDeliveryOptions);
        services.AddSingleton<ReportDatasetCatalog>();
        services.AddSingleton<IReportDatasetCatalog>(serviceProvider => serviceProvider.GetRequiredService<ReportDatasetCatalog>());
        services.AddSingleton<ReportExecutionGate>();
        services.AddScoped<IReportQueryExecutor, SqlReportQueryExecutor>();
        services.AddScoped<IWebhookHttpSender, WebhookHttpSender>();
        services.AddScoped<LocalFileStorageService>();
        if (string.Equals(fileStorageOptions.Provider, "Minio", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<MinioFileStorageService>();
            services.AddSingleton<IMinioClient>(_ => new MinioClient()
                .WithEndpoint(fileStorageOptions.Minio.Endpoint.Trim())
                .WithCredentials(fileStorageOptions.Minio.AccessKey, fileStorageOptions.Minio.SecretKey)
                .WithSSL(fileStorageOptions.Minio.UseSsl)
                .Build());
        }

        services.AddScoped<IFileStorageService>(serviceProvider =>
        {
            return string.Equals(fileStorageOptions.Provider, "Minio", StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<MinioFileStorageService>()
                : serviceProvider.GetRequiredService<LocalFileStorageService>();
        });

        services.AddCacheServices(configuration, environmentName);
        services.AddMessageBusServices(configuration);
        services.AddHangfireInfrastructure(configuration, connectionString);
        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>(
                "sql-server",
                tags: ["ready", "database", "sqlserver"])
            .AddCheck<NotificationDeliveryHealthCheck>(
                "notification-delivery",
                tags: ["ready", "notification", "messaging"]);

        if (string.Equals(fileStorageOptions.Provider, "Minio", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHealthChecks().AddCheck<MinioStorageHealthCheck>(
                "file-storage",
                tags: ["ready", "storage", "file", "minio"]);
        }
        else
        {
            services.AddHealthChecks().AddCheck<DiskStorageHealthCheck>(
                "file-storage",
                tags: ["ready", "storage", "file", "local"]);
        }

        var assemblies = new[] { typeof(DependencyInjection).Assembly }
            .Concat(moduleAssemblies)
            .ToArray();
        services.AddMarkedDependencies(assemblies);

        return services;
    }

    public static IServiceCollection AddMcpInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.Configure<OpenTelemetryOptions>(configuration.GetSection(OpenTelemetryOptions.SectionName));
        services.Configure<AiCenterOptions>(configuration.GetSection(AiCenterOptions.SectionName));
        services.AddSingleton<IAiToolConfiguration>(serviceProvider =>
            serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiCenterOptions>>().Value);
        services.AddSingleton<DbCommandMetricsInterceptor>();
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<DbCommandMetricsInterceptor>());
            options.UseOpenIddict();
        });
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IAsyncQueryExecutor, EfCoreAsyncQueryExecutor>();
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddCacheServices(configuration);
        services.AddScoped<ITenantStatusChecker, TenantStatusChecker>();
        services.AddScoped<IUserSessionStatusChecker, UserSessionStatusChecker>();
        services.AddScoped<IMcpClientBindingStore, McpClientBindingStore>();
        services.AddHealthChecks().AddCheck<SqlServerHealthCheck>(
            "sql-server",
            tags: ["ready", "database", "sqlserver"]);
        return services;
    }

    private static void ValidateReportOptions(ReportOptions options, string defaultConnection)
    {
        if (!options.SqlReportsEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ReportConnection))
        {
            throw new InvalidOperationException("Reports:ReportConnection must be configured when SQL reports are enabled.");
        }

        try
        {
            var reportConnection = new SqlConnectionStringBuilder(options.ReportConnection).ConnectionString;
            var applicationConnection = new SqlConnectionStringBuilder(defaultConnection).ConnectionString;
            var reportConnectionBuilder = new SqlConnectionStringBuilder(reportConnection);
            var applicationConnectionBuilder = new SqlConnectionStringBuilder(applicationConnection);
            if (string.IsNullOrWhiteSpace(reportConnectionBuilder.UserID) ||
                string.Equals(reportConnectionBuilder.UserID, applicationConnectionBuilder.UserID, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Reports:ReportConnection must use an isolated read-only database principal.");
            }
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("Reports:ReportConnection is invalid.", exception);
        }
    }

    public static IServiceCollection AddCacheServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string? environmentName = null)
    {
        services.AddMemoryCache();
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        var cacheOptions = configuration
            .GetSection(CacheOptions.SectionName)
            .Get<CacheOptions>() ?? new CacheOptions();
        var rateLimitOptions = configuration
            .GetSection(RateLimitOptions.SectionName)
            .Get<RateLimitOptions>() ?? new RateLimitOptions();

        if (cacheOptions.UseRedis() || rateLimitOptions.UseRedis())
        {
            services.AddRedisInfrastructure(configuration);
        }

        if (rateLimitOptions.UseRedis())
        {
            services.AddSingleton<IDistributedRateLimitService, RedisDistributedRateLimitService>();
        }
        else
        {
            services.AddSingleton<IDistributedRateLimitService, MemoryDistributedRateLimitService>();
        }

        if (cacheOptions.UseRedis())
        {
            services.AddSingleton<RedisCacheService>();
            services.AddSingleton<ICacheService>(serviceProvider =>
            {
                serviceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("PermissionSystem.Infrastructure.Caching")
                    .LogInformation("Using {Provider} cache provider.", CacheProviderNames.Redis);

                return serviceProvider.GetRequiredService<RedisCacheService>();
            });
            services.AddScoped<IIdempotencyService, RedisIdempotencyService>();
            services.AddScoped<IDistributedLock, RedisDistributedLock>();

            return services;
        }

        services.AddSingleton<MemoryCacheService>();
        services.AddSingleton<ICacheService>(serviceProvider =>
        {
            serviceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("PermissionSystem.Infrastructure.Caching")
                .LogInformation("Using {Provider} cache provider.", CacheProviderNames.Memory);

            return serviceProvider.GetRequiredService<MemoryCacheService>();
        });
        services.AddSingleton<IIdempotencyService, MemoryIdempotencyService>();
        services.AddSingleton<IDistributedLock, MemoryDistributedLock>();

        return services;
    }

    public static IServiceCollection AddHangfireWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var hangfireOptions = configuration
            .GetSection(HangfireOptions.SectionName)
            .Get<HangfireOptions>() ?? new HangfireOptions();

        if (!hangfireOptions.Enabled || !hangfireOptions.WorkerEnabled)
        {
            return services;
        }

        services.AddHangfireServer((serviceProvider, options) =>
        {
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
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
            redisConfiguration.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisConfiguration);
        });

        services.AddHealthChecks().AddCheck<RedisHealthCheck>(
            "redis",
            tags: ["ready", "cache", "redis"]);

        return services;
    }

    public static IServiceCollection AddMessageBusServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMQOptions>(configuration.GetSection(RabbitMQOptions.SectionName));

        var options = configuration
            .GetSection(RabbitMQOptions.SectionName)
            .Get<RabbitMQOptions>() ?? new RabbitMQOptions();

        if (!options.Enabled)
        {
            services.AddScoped<IMessageBus, NullMessageBus>();
            services.AddHealthChecks().AddCheck<RabbitMQDisabledHealthCheck>(
                "rabbitmq",
                tags: ["ready", "messaging", "rabbitmq"]);

            return services;
        }

        ValidateRabbitMqOptions(options);

        services.AddSingleton<IConnectionFactory>(_ =>
        {
            return new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(Math.Max(1, options.NetworkRecoveryIntervalSeconds)),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(Math.Max(1, options.ConnectionTimeoutSeconds))
            };
        });

        services.AddSingleton<RabbitMqConnectionManager>();
        services.AddSingleton<RabbitMqPublisherChannelPool>();
        services.AddScoped<IMessageBus, RabbitMqMessageBus>();
        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>(
            "rabbitmq",
            tags: ["ready", "messaging", "rabbitmq"]);

        return services;
    }

    private static void ValidateRabbitMqOptions(RabbitMQOptions options)
    {
        if (!options.EnablePublisherConfirms)
        {
            throw new InvalidOperationException("RabbitMQ publisher confirms must be enabled when RabbitMQ is enabled.");
        }

        if (options.PrefetchCount == 0)
        {
            throw new InvalidOperationException("RabbitMQ PrefetchCount must be greater than zero.");
        }

        if (options.ConsumerRetryCount < 0 || options.ConsumerRetryDelaySeconds <= 0)
        {
            throw new InvalidOperationException("RabbitMQ consumer retry configuration is invalid.");
        }
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

        if (!hangfireOptions.Enabled)
        {
            services.AddScoped<IBackgroundJobService, DisabledBackgroundJobService>();
            services.AddHealthChecks().AddCheck<HangfireDisabledHealthCheck>(
                "hangfire",
                tags: ["ready", "background-jobs", "hangfire"]);

            return services;
        }

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
        services.AddHealthChecks().AddCheck<HangfireHealthCheck>(
            "hangfire",
            tags: ["ready", "background-jobs", "hangfire"]);

        return services;
    }

    private static void ValidateNotificationDeliveryOptions(
        NotificationDeliveryOptions notificationOptions,
        IConfiguration configuration)
    {
        if (notificationOptions.DeliveryMode != NotificationDeliveryMode.OutboxRabbitMQ)
        {
            return;
        }

        var rabbitMqOptions = configuration
            .GetSection(RabbitMQOptions.SectionName)
            .Get<RabbitMQOptions>() ?? new RabbitMQOptions();

        if (!rabbitMqOptions.Enabled ||
            !rabbitMqOptions.EnableConsumers ||
            !rabbitMqOptions.EnableOutboxPublisher)
        {
            throw new InvalidOperationException(
                "Notifications:DeliveryMode=OutboxRabbitMQ requires RabbitMQ:Enabled, RabbitMQ:EnableConsumers, and RabbitMQ:EnableOutboxPublisher to be true.");
        }
    }
}
