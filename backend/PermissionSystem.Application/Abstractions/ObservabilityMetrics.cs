using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PermissionSystem.Application.Abstractions;

public static class ObservabilityMetrics
{
    public const string MeterName = "PermissionSystem.Metrics";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> HttpRequests = Meter.CreateCounter<long>("permission_system.http.server.requests");
    private static readonly Histogram<double> HttpRequestDuration = Meter.CreateHistogram<double>("permission_system.http.server.request.duration", "s");
    private static readonly Counter<long> LoginAttempts = Meter.CreateCounter<long>("permission_system.authentication.login.attempts");
    private static readonly Counter<long> LoginLockouts = Meter.CreateCounter<long>("permission_system.authentication.login.lockouts");
    private static readonly Histogram<double> DatabaseCommandDuration = Meter.CreateHistogram<double>("permission_system.database.command.duration", "s");
    private static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>("permission_system.outbox.messages.published");
    private static readonly Counter<long> OutboxRetries = Meter.CreateCounter<long>("permission_system.outbox.messages.retried");
    private static readonly Counter<long> OutboxFailures = Meter.CreateCounter<long>("permission_system.outbox.messages.failed");
    private static readonly Counter<long> FileScanFailures = Meter.CreateCounter<long>("permission_system.file.scan.failures");
    private static long _outboxPending;
    private static long _hangfireQueueLength;
    private static long _hangfireServerCount;
    private static long _fileStorageAvailableBytes;

    static ObservabilityMetrics()
    {
        Meter.CreateObservableGauge("permission_system.outbox.messages.pending", () => Volatile.Read(ref _outboxPending));
        Meter.CreateObservableGauge("permission_system.hangfire.queue.length", () => Volatile.Read(ref _hangfireQueueLength));
        Meter.CreateObservableGauge("permission_system.hangfire.servers", () => Volatile.Read(ref _hangfireServerCount));
        Meter.CreateObservableGauge("permission_system.file_storage.available_bytes", () => Volatile.Read(ref _fileStorageAvailableBytes));
    }

    public static void RecordHttpRequest(string method, string route, int statusCode, TimeSpan elapsed)
    {
        var tags = new TagList
        {
            { "http.request.method", method },
            { "http.route", route },
            { "http.response.status_code", statusCode }
        };

        HttpRequests.Add(1, tags);
        HttpRequestDuration.Record(elapsed.TotalSeconds, tags);
    }

    public static void RecordLoginAttempt(string outcome)
    {
        LoginAttempts.Add(1, new TagList { { "auth.outcome", outcome } });
    }

    public static void RecordLoginLockout()
    {
        LoginLockouts.Add(1);
    }

    public static void RecordDatabaseCommand(TimeSpan elapsed)
    {
        DatabaseCommandDuration.Record(elapsed.TotalSeconds, new TagList { { "db.system", "mssql" } });
    }

    public static void RecordOutboxBacklog(long pendingCount)
    {
        Interlocked.Exchange(ref _outboxPending, Math.Max(0, pendingCount));
    }

    public static void RecordOutboxPublished()
    {
        OutboxPublished.Add(1);
    }

    public static void RecordOutboxRetry()
    {
        OutboxRetries.Add(1);
    }

    public static void RecordOutboxFailure()
    {
        OutboxFailures.Add(1);
    }

    public static void RecordHangfireState(long queueLength, long serverCount)
    {
        Interlocked.Exchange(ref _hangfireQueueLength, Math.Max(0, queueLength));
        Interlocked.Exchange(ref _hangfireServerCount, Math.Max(0, serverCount));
    }

    public static void RecordFileStorageAvailableBytes(long availableBytes)
    {
        Interlocked.Exchange(ref _fileStorageAvailableBytes, Math.Max(0, availableBytes));
    }

    public static void RecordFileScanFailure()
    {
        FileScanFailures.Add(1);
    }
}
