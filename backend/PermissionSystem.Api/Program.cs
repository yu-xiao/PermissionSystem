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
using PermissionSystem.Api.Authentication;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Api.Services;
using PermissionSystem.Application;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Infrastructure.SeedData;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

const string CorsPolicyName = "PermissionSystemCors";

builder.Services.AddControllers()
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
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthenticationResultHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<AppDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetRevocationEndpointUris("/connect/revoke")
            .SetEndSessionEndpointUris("/connect/logout");

        options.AllowPasswordFlow()
            .AllowRefreshTokenFlow()
            .AllowClientCredentialsFlow()
            .AllowAuthorizationCodeFlow()
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
builder.Services.AddHealthChecks();

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
}

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
        options.OAuthClientSecret("permission-admin-secret");
        options.OAuthUsePkce();
        options.OAuthScopes("permission-system-api", OpenIddictConstants.Scopes.OfflineAccess);
    });
}

app.UseHttpsRedirection();

app.UseCors(CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

var hangfireOptions = app.Services.GetRequiredService<IOptions<HangfireOptions>>().Value;
app.UseHangfireDashboard(
    hangfireOptions.DashboardPath,
    new DashboardOptions
    {
        Authorization =
        [
            new HangfireDashboardAuthorizationFilter(app.Environment)
        ]
    });

app.MapControllers();

app.Run();
