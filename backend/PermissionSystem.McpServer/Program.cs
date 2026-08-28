using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ModelContextProtocol.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using PermissionSystem.Application;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure;
using PermissionSystem.McpServer.Configuration;
using PermissionSystem.McpServer.Middlewares;
using PermissionSystem.McpServer.Services;
using PermissionSystem.McpServer.Tools;
using PermissionSystem.Shared.Constants;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Development.local.json",
        optional: true,
        reloadOnChange: true);
}

var authenticationOptions = McpStartupValidator.Validate(builder.Configuration, builder.Environment);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, McpCurrentUserService>();
builder.Services.AddAiCenterCore();
builder.Services.AddMcpInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});
builder.Services.AddOpenIddict().AddValidation(options =>
{
    options.SetIssuer(new Uri(authenticationOptions.Authority));
    options.AddAudiences(AiCenterConstants.McpResource);
    options.UseIntrospection()
        .SetClientId(authenticationOptions.IntrospectionClientId)
        .SetClientSecret(authenticationOptions.IntrospectionClientSecret);
    options.UseSystemNetHttp();
    options.UseAspNetCore();
});
builder.Services.Configure<AuthenticationOptions>(options =>
{
    options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpAccess", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => context.User.HasScope(AiCenterConstants.McpScope));
    });
});
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<DatasetTools>();
builder.Services.AddHealthChecks().AddCheck(
    "mcp-self",
    () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("MCP server is running."),
    tags: ["live"]);

var app = builder.Build();

app.UseMiddleware<McpTraceIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/mcp"),
    secured =>
    {
        secured.UseAuthentication();
        secured.UseMiddleware<McpCallerValidationMiddleware>();
        secured.UseAuthorization();
    });

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();
app.MapMcp("/mcp").RequireAuthorization("McpAccess");

app.Run();

public partial class Program;
