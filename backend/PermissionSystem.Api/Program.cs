using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PermissionSystem.Api.Authentication;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Configuration;
using PermissionSystem.Api.Hubs;
using PermissionSystem.Api.HealthChecks;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Api.RateLimiting;
using PermissionSystem.Api.Services;
using PermissionSystem.Application;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Files;
using PermissionSystem.Application.Messaging;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Infrastructure.SeedData;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;
using Serilog;
using StackExchange.Redis;
using AppRateLimitOptions = PermissionSystem.Infrastructure.Options.RateLimitOptions;
using AppOpenTelemetryOptions = PermissionSystem.Infrastructure.Options.OpenTelemetryOptions;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Development.local.json",
        optional: true,
        reloadOnChange: true);
}

StartupSecurityValidator.ValidateProductionConfiguration(builder.Configuration, builder.Environment);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

const string CorsPolicyName = "PermissionSystemCors";
var reverseProxyOptions = builder.Services.AddConfiguredForwardedHeaders(builder.Configuration);

builder.Services.AddScoped<IdempotencyFilter>();
builder.Services.AddScoped<PreventDuplicateSubmitFilter>();
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<IdempotencyFilter>();
        options.Filters.Add<PreventDuplicateSubmitFilter>();
        options.Conventions.Add(new ApiVersionRouteConvention());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressMapClientErrors = true;
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            return new UnprocessableEntityObjectResult(ApiResult<object>.Fail(
                ErrorCode.ValidationFailed,
                "Request validation failed.",
                context.HttpContext.TraceIdentifier,
                errors));
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PermissionSystem API",
        Version = "v1",
        Description = "Enterprise permission management platform API."
    });
    options.DocumentFilter<ApiVersionOpenApiDocumentFilter>();

    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            Password = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri("/connect/token", UriKind.Relative),
                RefreshUrl = new Uri("/connect/token", UriKind.Relative),
                Scopes = new Dictionary<string, string>
                {
                    [OpenIddictConstants.Scopes.OpenId] = "OpenID Connect",
                    [OpenIddictConstants.Scopes.Profile] = "User profile",
                    [OpenIddictConstants.Scopes.OfflineAccess] = "Refresh token",
                    ["permission-system-api"] = "PermissionSystem API"
                }
            },
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri("/connect/token", UriKind.Relative),
                Scopes = new Dictionary<string, string>
                {
                    ["permission-system-api"] = "PermissionSystem API"
                }
            },
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri("/connect/authorize", UriKind.Relative),
                TokenUrl = new Uri("/connect/token", UriKind.Relative),
                RefreshUrl = new Uri("/connect/token", UriKind.Relative),
                Scopes = new Dictionary<string, string>
                {
                    [OpenIddictConstants.Scopes.OpenId] = "OpenID Connect",
                    [OpenIddictConstants.Scopes.Profile] = "User profile",
                    [OpenIddictConstants.Scopes.OfflineAccess] = "Refresh token",
                    ["permission-system-api"] = "PermissionSystem API"
                }
            }
        }
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("oauth2", document, null)] = ["permission-system-api"]
    });
});
builder.Services.AddCors(options =>
{
    var allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
    });
});
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClientIpAccessor, ClientIpAccessor>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditContext, CurrentAuditContext>();
builder.Services.AddScoped<PermissionSystem.Application.Security.ISensitiveOperationCodeProvider, SensitiveOperationCodeProvider>();
builder.Services.AddScoped<ITenantResolver, TenantResolver>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthenticationResultHandler>();
var rabbitMqOptions = builder.Configuration
    .GetSection(RabbitMQOptions.SectionName)
    .Get<RabbitMQOptions>() ?? new RabbitMQOptions();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.EnvironmentName);
builder.Services.AddApplication(rabbitMqOptions.Enabled && rabbitMqOptions.EnableOutboxPublisher);
builder.Services.AddScoped<INotificationRealtimeSender, SignalRNotificationRealtimeSender>();
builder.Services.AddScoped<IAiRunRealtimeSender, SignalRAiRunRealtimeSender>();
if (rabbitMqOptions.Enabled && rabbitMqOptions.EnableConsumers)
{
    builder.Services.AddHostedService<NotificationEventConsumerHostedService>();
}
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<AppDbContext>();
    })
    .AddServer(options =>
    {
        var configuredIssuer = builder.Configuration["OpenIddict:Issuer"];
        if (!string.IsNullOrWhiteSpace(configuredIssuer))
        {
            options.SetIssuer(new Uri(configuredIssuer, UriKind.Absolute));
        }

        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token", "/api/sso/oidc/exchange")
            .SetIntrospectionEndpointUris("/connect/introspect")
            .SetRevocationEndpointUris("/connect/revoke")
            .SetEndSessionEndpointUris("/connect/logout");

        options.AllowPasswordFlow()
            .AllowRefreshTokenFlow()
            .AllowClientCredentialsFlow()
            .AllowAuthorizationCodeFlow()
            .AllowCustomFlow(PermissionSystem.Application.Sso.SsoGrantTypes.OidcLoginCode)
            .RequireProofKeyForCodeExchange();

        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.OfflineAccess,
            OpenIddictConstants.Scopes.Roles,
            AiCenterConstants.ApiResource,
            AiCenterConstants.McpScope);

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(
            builder.Configuration.GetValue("OpenIddict:AccessTokenMinutes", 60)));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(
            builder.Configuration.GetValue("OpenIddict:RefreshTokenDays", 14)));

        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        var aspNetCoreBuilder = options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough();

        if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Docker"))
        {
            aspNetCoreBuilder.DisableTransportSecurityRequirement();
        }
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.AddAudiences(AiCenterConstants.ApiResource);
        options.UseAspNetCore();
    });
builder.Services.Configure<AuthenticationOptions>(options =>
{
    options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});
builder.Services.AddHealthChecks().AddCheck(
    "api-self",
    () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is running."),
    tags: ["live"]);
ConfigureOpenTelemetry(builder.Services, builder.Configuration);

var app = builder.Build();
var hangfireOptions = app.Services.GetRequiredService<IOptions<HangfireOptions>>().Value;

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var seedDataInitializer = scope.ServiceProvider.GetRequiredService<SeedDataInitializer>();
    await seedDataInitializer.InitializeAsync();
}

if (hangfireOptions.Enabled)
{
    using var scope = app.Services.CreateScope();
    var scheduledTaskService = scope.ServiceProvider.GetRequiredService<IScheduledTaskService>();
    await scheduledTaskService.SyncEnabledTasksAsync();

    var backgroundJobService = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
    if (rabbitMqOptions.Enabled && rabbitMqOptions.EnableOutboxPublisher)
    {
        backgroundJobService.AddOrUpdateRecurring<OutboxPublisherJob>(
            "outbox:publisher",
            job => job.ExecuteAsync(),
            "* * * * *",
            TimeZoneInfo.Local,
            "default");
    }
    else
    {
        backgroundJobService.RemoveRecurring("outbox:publisher");
    }

    backgroundJobService.AddOrUpdateRecurring<FileStorageCompensationJob>(
        "files:storage-compensation",
        job => job.ExecuteAsync(),
        "*/5 * * * *",
        TimeZoneInfo.Local,
        "default");
}

if (reverseProxyOptions.Enabled)
{
    app.UseForwardedHeaders();
}

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseMiddleware<SecurityHeadersMiddleware>();
}

app.UseHttpsRedirection();

app.UseMiddleware<LegacyApiRouteDeprecationMiddleware>();

app.UseMiddleware<TraceIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PermissionSystem API v1");
        options.RoutePrefix = "swagger";
        options.OAuthClientId("permission-admin");
        var swaggerClientSecret = builder.Configuration["SeedData:OAuthClientSecret"];
        if (!string.IsNullOrWhiteSpace(swaggerClientSecret))
        {
            options.OAuthClientSecret(swaggerClientSecret);
        }
        options.OAuthUsePkce();
        options.OAuthScopes("permission-system-api", OpenIddictConstants.Scopes.OfflineAccess);
    });
}

app.UseRouting();

app.UseCors(CorsPolicyName);

app.UseMiddleware<RequestMetricsMiddleware>();

app.UseWhen(
    context => !IsAnonymousHealthProbe(context.Request.Path),
    secured =>
    {
        secured.UseMiddleware<SignalRAccessTokenMiddleware>();
        secured.UseAuthentication();
        secured.UseMiddleware<TokenRateLimitMetadataMiddleware>();
        secured.UseMiddleware<DistributedRateLimitMiddleware>();
        secured.UseMiddleware<TenantMiddleware>();
        secured.UseMiddleware<UserSessionMiddleware>();
        secured.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        secured.UseMiddleware<TenantStatusMiddleware>();
        secured.UseMiddleware<IpAccessMiddleware>();
        secured.UseAuthorization();
        secured.UseMiddleware<OperationLogMiddleware>();
    });

if (hangfireOptions.Enabled && hangfireOptions.DashboardEnabled)
{
    app.UseHangfireDashboard(
        hangfireOptions.DashboardPath,
        new DashboardOptions
        {
            Authorization =
            [
                new HangfireDashboardAuthorizationFilter()
            ]
        });
}

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<AiHub>("/hubs/ai");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = HealthCheckEndpointPredicates.IsLive
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = HealthCheckEndpointPredicates.IsReady
}).AllowAnonymous();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = HealthCheckEndpointPredicates.IsReady,
    ResponseWriter = HealthCheckResponseWriter.WriteSummaryAsync
}).AllowAnonymous();
app.MapHealthChecks("/api/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = HealthCheckEndpointPredicates.IsReady,
    ResponseWriter = HealthCheckResponseWriter.WriteSummaryAsync
}).AllowAnonymous();

app.Run();

static void ConfigureOpenTelemetry(IServiceCollection services, IConfiguration configuration)
{
    var settings = configuration
        .GetSection(AppOpenTelemetryOptions.SectionName)
        .Get<AppOpenTelemetryOptions>() ?? new AppOpenTelemetryOptions();

    services.Configure<AppOpenTelemetryOptions>(configuration.GetSection(AppOpenTelemetryOptions.SectionName));
    if (!settings.Enabled)
    {
        return;
    }

    var cacheOptions = configuration
        .GetSection(CacheOptions.SectionName)
        .Get<CacheOptions>() ?? new CacheOptions();
    var otlpEndpoint = string.IsNullOrWhiteSpace(settings.OtlpEndpoint)
        ? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
        : settings.OtlpEndpoint;

    services
        .AddOpenTelemetry()
        .ConfigureResource(resource =>
        {
            resource.AddService(
                serviceName: string.IsNullOrWhiteSpace(settings.ServiceName) ? "PermissionSystem.Api" : settings.ServiceName,
                serviceVersion: string.IsNullOrWhiteSpace(settings.ServiceVersion) ? "1.0.0" : settings.ServiceVersion);
        })
        .WithTracing(tracing =>
        {
            tracing
                .SetSampler(new TraceIdRatioBasedSampler(Math.Clamp(settings.SamplingRatio, 0, 1)))
                .AddSource(TraceActivitySources.Messaging)
                .AddSource(TraceActivitySources.BackgroundJobs)
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        if (request.Headers.TryGetValue(TraceIdMiddleware.TraceHeaderName, out var traceId))
                        {
                            activity.SetTag("app.trace_id", traceId.ToString());
                        }
                    };
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    if (settings.IncludeSqlStatements)
                    {
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.statement", command.CommandText);
                        };
                    }
                });

            if (cacheOptions.UseRedis())
            {
                tracing
                    .AddRedisInstrumentation(options =>
                    {
                        options.SetVerboseDatabaseStatements = settings.IncludeRedisStatements;
                    })
                    .ConfigureRedisInstrumentation((serviceProvider, instrumentation) =>
                    {
                        instrumentation.AddConnection(serviceProvider.GetRequiredService<IConnectionMultiplexer>());
                    });
            }

            if (settings.ConsoleExporterEnabled)
            {
                tracing.AddConsoleExporter();
            }

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            }
        })
        .WithMetrics(metrics =>
        {
            if (!settings.MetricsEnabled)
            {
                return;
            }

            metrics
                .AddMeter(ObservabilityMetrics.MeterName)
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter("System.Net.Http")
                .AddMeter("System.Runtime");

            if (settings.ConsoleExporterEnabled)
            {
                metrics.AddConsoleExporter();
            }

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                metrics.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            }
        });
}

static bool IsAnonymousHealthProbe(PathString path)
{
    return path.Equals("/health/live", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/health", StringComparison.OrdinalIgnoreCase);
}

public partial class Program
{
}
