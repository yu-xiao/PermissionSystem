using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.Infrastructure.Observability;

public sealed class DbCommandMetricsInterceptor : DbCommandInterceptor
{
    private readonly OpenTelemetryOptions _options;
    private readonly ILogger<DbCommandMetricsInterceptor> _logger;

    public DbCommandMetricsInterceptor(
        IOptions<OpenTelemetryOptions> options,
        ILogger<DbCommandMetricsInterceptor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Record(command, eventData.Duration);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        Record(command, eventData.Duration);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        Record(command, eventData.Duration);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void Record(DbCommand command, TimeSpan duration)
    {
        ObservabilityMetrics.RecordDatabaseCommand(duration);
        if (duration.TotalMilliseconds < Math.Max(1, _options.SlowSqlThresholdMilliseconds))
        {
            return;
        }

        if (_options.IncludeSqlStatements)
        {
            _logger.LogWarning(
                "Slow SQL command detected. DurationMs: {DurationMs}, CommandType: {CommandType}, CommandText: {CommandText}",
                Math.Round(duration.TotalMilliseconds, 2),
                command.CommandType,
                command.CommandText);
            return;
        }

        _logger.LogWarning(
            "Slow SQL command detected. DurationMs: {DurationMs}, CommandType: {CommandType}",
            Math.Round(duration.TotalMilliseconds, 2),
            command.CommandType);
    }
}
