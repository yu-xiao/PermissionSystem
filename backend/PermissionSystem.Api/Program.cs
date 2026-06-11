using System.Text.Json;
using System.Threading.RateLimiting;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PermissionSystem.Api.Authentication;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Hubs;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Api.RateLimiting;
using PermissionSystem.Api.Services;
using PermissionSystem.Application;
using PermissionSystem.Application.Abstractions;
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

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

const string CorsPolicyName = "PermissionSystemCors";

builder.Services.AddScoped<IdempotencyFilter>();
builder.Services.AddScoped<PreventDuplicateSubmitFilter>();
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<IdempotencyFilter>();
        options.Filters.Add<PreventDuplicateSubmitFilter>();
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

            return new BadRequestObjectResult(ApiResult<object>.Fail(
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
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();

            return;
        }

        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<PermissionSystem.Application.Security.ISensitiveOperationCodeProvider, SensitiveOperationCodeProvider>();
builder.Services.AddScoped<ITenantResolver, TenantResolver>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthenticationResultHandler>();
var rabbitMqOptions = builder.Configuration
    .GetSection(RabbitMQOptions.SectionName)
    .Get<RabbitMQOptions>() ?? new RabbitMQOptions();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(rabbitMqOptions.Enabled && rabbitMqOptions.EnableOutboxPublisher);
builder.Services.AddScoped<INotificationRealtimeSender, SignalRNotificationRealtimeSender>();
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
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token", "/api/sso/oidc/exchange")
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
            "permission-system-api");

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
    tags: ["self"]);
ConfigureRateLimiting(builder.Services, builder.Configuration);
ConfigureOpenTelemetry(builder.Services, builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var seedDataInitializer = scope.ServiceProvider.GetRequiredService<SeedDataInitializer>();
    await seedDataInitializer.InitializeAsync();
}

using (var scope = app.Services.CreateScope())
{
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
}

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

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(CorsPolicyName);

app.UseMiddleware<SignalRAccessTokenMiddleware>();
app.UseAuthentication();
app.UseMiddleware<UserSessionMiddleware>();
app.UseMiddleware<TokenRateLimitMetadataMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<TenantMiddleware>();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseMiddleware<IpAccessMiddleware>();
app.UseAuthorization();
app.UseMiddleware<OperationLogMiddleware>();

var hangfireOptions = app.Services.GetRequiredService<IOptions<HangfireOptions>>().Value;
app.UseHangfireDashboard(
    hangfireOptions.DashboardPath,
    new DashboardOptions
    {
        Authorization =
        [
            new HangfireDashboardAuthorizationFilter()
        ]
    });

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

static void ConfigureRateLimiting(IServiceCollection services, IConfiguration configuration)
{
    var settings = configuration
        .GetSection(AppRateLimitOptions.SectionName)
        .Get<AppRateLimitOptions>() ?? new AppRateLimitOptions();

    services.Configure<AppRateLimitOptions>(configuration.GetSection(AppRateLimitOptions.SectionName));
    services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = WriteRateLimitRejectedAsync;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!settings.Enabled || IsRateLimitExempt(context.Request.Path))
            {
                return RateLimitPartition.GetNoLimiter("exempt");
            }

            return CreateFixedWindowPartition(
                context,
                "global",
                settings.GlobalPermitLimit,
                settings.GlobalWindowSeconds,
                settings.QueueLimit);
        });

        options.AddPolicy(RateLimitPolicyNames.Token, context =>
        {
            if (!settings.Enabled)
            {
                return RateLimitPartition.GetNoLimiter("token-disabled");
            }

            var grantType = context.Items[RateLimitMetadataKeys.GrantType] as string;
            if (string.Equals(grantType, OpenIddictConstants.GrantTypes.Password, StringComparison.OrdinalIgnoreCase))
            {
                return CreateFixedWindowPartition(
                    context,
                    "login",
                    settings.LoginPermitLimit,
                    settings.LoginWindowSeconds,
                    settings.QueueLimit);
            }

            if (string.Equals(grantType, OpenIddictConstants.GrantTypes.RefreshToken, StringComparison.OrdinalIgnoreCase))
            {
                return CreateFixedWindowPartition(
                    context,
                    "refresh-token",
                    settings.RefreshTokenPermitLimit,
                    settings.RefreshTokenWindowSeconds,
                    settings.QueueLimit);
            }

            return CreateFixedWindowPartition(
                context,
                "token",
                settings.GlobalPermitLimit,
                settings.GlobalWindowSeconds,
                settings.QueueLimit);
        });
    });
}

static ValueTask WriteRateLimitRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
{
    var httpContext = context.HttpContext;
    var logger = httpContext.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("PermissionSystem.Api.RateLimiting");

    var partitionKey = BuildRateLimitIdentityKey(httpContext);
    logger.LogWarning(
        "Rate limit rejected. Method: {Method}, Path: {Path}, PartitionKey: {PartitionKey}, TraceId: {TraceId}",
        httpContext.Request.Method,
        httpContext.Request.Path,
        partitionKey,
        httpContext.TraceIdentifier);

    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
    {
        httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
    }

    httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    httpContext.Response.ContentType = "application/json; charset=utf-8";

    var result = ApiResult.Fail(
        ErrorCode.TooManyRequests,
        "Too many requests. Please try again later.",
        httpContext.TraceIdentifier);

    return new ValueTask(httpContext.Response.WriteAsync(
        JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        cancellationToken));
}

static RateLimitPartition<string> CreateFixedWindowPartition(
    HttpContext context,
    string policyName,
    int permitLimit,
    int windowSeconds,
    int queueLimit)
{
    var partitionKey = $"{policyName}:{BuildRateLimitIdentityKey(context)}";
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, permitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = Math.Max(0, queueLimit),
            AutoReplenishment = true
        });
}

static string BuildRateLimitIdentityKey(HttpContext context)
{
    var userId = context.User.FindFirst(ClaimConstants.UserId)?.Value;
    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"user:{userId}";
    }

    var clientId = context.Items[RateLimitMetadataKeys.ClientId] as string
        ?? context.User.FindFirst(OpenIddictConstants.Claims.ClientId)?.Value;
    if (!string.IsNullOrWhiteSpace(clientId))
    {
        return $"client:{clientId.Trim()}";
    }

    return $"ip:{GetClientIp(context)}";
}

static string GetClientIp(HttpContext context)
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        return forwardedFor.Split(',')[0].Trim();
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static bool IsRateLimitExempt(PathString path)
{
    return path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);
}

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

            if (!string.IsNullOrWhiteSpace(settings.OtlpEndpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(settings.OtlpEndpoint);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            }
        });
}

public partial class Program
{
}
