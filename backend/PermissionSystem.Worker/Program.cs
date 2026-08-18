using PermissionSystem.Application;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure;
using PermissionSystem.Infrastructure.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<OpenTelemetryOptions>(builder.Configuration.GetSection(OpenTelemetryOptions.SectionName));
builder.Services.AddSerilog((services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

var rabbitMqOptions = builder.Configuration
    .GetSection(RabbitMQOptions.SectionName)
    .Get<RabbitMQOptions>() ?? new RabbitMQOptions();

builder.Services.AddApplication(rabbitMqOptions.Enabled && rabbitMqOptions.EnableOutboxPublisher);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.EnvironmentName);
var hangfireOptions = builder.Configuration
    .GetSection(HangfireOptions.SectionName)
    .Get<HangfireOptions>() ?? new HangfireOptions();
if (hangfireOptions.Enabled && hangfireOptions.WorkerEnabled)
{
    builder.Services.AddHangfireWorker(builder.Configuration);
}
ConfigureOpenTelemetry(builder.Services, builder.Configuration);

var host = builder.Build();
host.Run();

static void ConfigureOpenTelemetry(IServiceCollection services, IConfiguration configuration)
{
    var settings = configuration
        .GetSection(OpenTelemetryOptions.SectionName)
        .Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions
        {
            ServiceName = "PermissionSystem.Worker"
        };

    if (!settings.Enabled)
    {
        return;
    }

    var otlpEndpoint = string.IsNullOrWhiteSpace(settings.OtlpEndpoint)
        ? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
        : settings.OtlpEndpoint;

    services
        .AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: string.IsNullOrWhiteSpace(settings.ServiceName) ? "PermissionSystem.Worker" : settings.ServiceName,
            serviceVersion: string.IsNullOrWhiteSpace(settings.ServiceVersion) ? "1.0.0" : settings.ServiceVersion))
        .WithTracing(tracing =>
        {
            tracing
                .SetSampler(new TraceIdRatioBasedSampler(Math.Clamp(settings.SamplingRatio, 0, 1)))
                .AddSource(TraceActivitySources.Messaging)
                .AddSource(TraceActivitySources.BackgroundJobs);

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
