namespace PermissionSystem.Infrastructure.Options;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled { get; init; } = true;

    public string ServiceName { get; init; } = "PermissionSystem.Api";

    public string ServiceVersion { get; init; } = "1.0.0";

    public bool ConsoleExporterEnabled { get; init; }

    public string? OtlpEndpoint { get; init; }

    public bool MetricsEnabled { get; init; } = true;

    public int SlowSqlThresholdMilliseconds { get; init; } = 1000;

    public double SamplingRatio { get; init; } = 1.0;

    public bool IncludeSqlStatements { get; init; }

    public bool IncludeRedisStatements { get; init; }
}
