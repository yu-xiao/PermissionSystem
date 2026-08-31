using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using PermissionSystem.Application;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Departments;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Application.Reports;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Infrastructure;
using PermissionSystem.Infrastructure.Ai;
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
var protectedResourceMetadata = new McpProtectedResourceMetadata(authenticationOptions);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(protectedResourceMetadata);
builder.Services.AddScoped<ICurrentUserService, McpCurrentUserService>();
builder.Services.AddAiCenterCore();
builder.Services.AddMcpInfrastructure(builder.Configuration);
builder.Services.AddScoped<ITenantWriteResolver, TenantWriteResolver>();
builder.Services.AddScoped<DataScopeService>();
builder.Services.AddScoped<IDataScopeService>(serviceProvider =>
    serviceProvider.GetRequiredService<DataScopeService>());
builder.Services.AddScoped<IDataPermissionFilter, DataPermissionFilter>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IReadOnlyReportQueryService, DisabledReadOnlyReportQueryService>();
builder.Services.AddScoped<IAiReadOnlyToolRegistry, AiReadOnlyToolRegistry>();
builder.Services.AddScoped<IMcpCallerContext, McpCallerContext>();
builder.Services.AddScoped<IMcpClientAccessService, McpClientAccessService>();
builder.Services.AddScoped<IMcpDatasetService, McpDatasetService>();
builder.Services.AddScoped<IMcpDatasetQueryHandler, PlatformCapabilitiesMcpDatasetQueryHandler>();
builder.Services.AddScoped<IMcpDatasetQueryHandler, DepartmentDirectoryMcpDatasetQueryHandler>();
builder.Services.AddScoped<IMcpDatasetQueryHandlerResolver, McpDatasetQueryHandlerResolver>();
builder.Services.AddScoped<IAiAlertService, AiAlertService>();
builder.Services.AddScoped<IAiCircuitBreaker, AiCircuitBreaker>();

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
    .WithTools<DatasetTools>()
    .WithTools<PermissionReadOnlyTools>()
    .WithRequestFilters(filters => filters.AddListToolsFilter(next => async (context, cancellationToken) =>
    {
        var result = await next(context, cancellationToken);
        var services = context.Server.Services
            ?? throw new InvalidOperationException("MCP request services are unavailable.");
        var callerContext = services.GetRequiredService<IMcpCallerContext>();
        if (callerContext.CallerType == PermissionSystem.Domain.Enums.McpCallerType.ServiceClient)
        {
            result.Tools = result.Tools
                .Where(tool => tool.Name is "list_datasets" or "describe_dataset" or "query_dataset")
                .ToList();
        }

        return result;
    }));
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
        secured.UseMiddleware<McpResourceMetadataChallengeMiddleware>();
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
app.MapGet(
        "/.well-known/oauth-protected-resource",
        (McpProtectedResourceMetadata metadata) => Results.Json(metadata.Document))
    .AllowAnonymous();
app.MapGet(
        "/.well-known/oauth-protected-resource/mcp",
        (McpProtectedResourceMetadata metadata) => Results.Json(metadata.Document))
    .AllowAnonymous();
app.MapMcp("/mcp").RequireAuthorization("McpAccess");

app.Run();

public partial class Program;
