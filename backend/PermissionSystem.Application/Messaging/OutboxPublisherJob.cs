using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Jobs;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;

namespace PermissionSystem.Application.Messaging;

public sealed class OutboxPublisherJob
{
    private const int BatchSize = 50;
    private const int MaxRetryCount = 5;
    private static readonly ActivitySource ActivitySource = new(TraceActivitySources.BackgroundJobs);

    private readonly IRepository<OutboxMessage> _outboxRepository;
    private readonly IRepository<JobExecutionLog> _jobExecutionLogRepository;
    private readonly IMessageBus _messageBus;
    private readonly IDistributedLock _distributedLock;
    private readonly ITraceContextAccessor _traceContextAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OutboxPublisherJob> _logger;

    public OutboxPublisherJob(
        IRepository<OutboxMessage> outboxRepository,
        IRepository<JobExecutionLog> jobExecutionLogRepository,
        IMessageBus messageBus,
        IDistributedLock distributedLock,
        ITraceContextAccessor traceContextAccessor,
        IUnitOfWork unitOfWork,
        ILogger<OutboxPublisherJob> logger)
    {
        _outboxRepository = outboxRepository;
        _jobExecutionLogRepository = jobExecutionLogRepository;
        _messageBus = messageBus;
        _distributedLock = distributedLock;
        _traceContextAccessor = traceContextAccessor;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var traceId = EnsureTraceId();
        using var activity = StartJobActivity(traceId, "hangfire.outbox.publisher");
        using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        if (!_messageBus.IsOutboxPublisherEnabled)
        {
            _logger.LogInformation("Outbox publisher skipped because RabbitMQ outbox publishing is disabled. TraceId: {TraceId}", traceId);
            await RecordJobExecutionLogAsync(
                JobExecutionStatuses.Skipped,
                startedAt,
                stopwatch,
                "RabbitMQ outbox publisher is disabled.",
                traceId);
            return;
        }

        try
        {
            await _distributedLock.ExecuteWithLockAsync(
                "outbox:publisher",
                PublishPendingAsync,
                TimeSpan.FromMinutes(5),
                TimeSpan.Zero);

            await RecordJobExecutionLogAsync(
                JobExecutionStatuses.Succeeded,
                startedAt,
                stopwatch,
                null,
                traceId);
        }
        catch (TimeoutException exception)
        {
            _logger.LogInformation("Outbox publisher skipped because a distributed lock is held. TraceId: {TraceId}", traceId);
            await RecordJobExecutionLogAsync(
                JobExecutionStatuses.Skipped,
                startedAt,
                stopwatch,
                exception.Message,
                traceId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Outbox publisher failed. TraceId: {TraceId}", traceId);
            await RecordJobExecutionLogAsync(
                JobExecutionStatuses.Failed,
                startedAt,
                stopwatch,
                exception.Message,
                traceId);
            throw;
        }
    }

    private async Task RecordJobExecutionLogAsync(
        string status,
        DateTimeOffset startedAt,
        Stopwatch stopwatch,
        string? errorMessage,
        string traceId)
    {
        stopwatch.Stop();
        await _jobExecutionLogRepository.AddAsync(new JobExecutionLog
        {
            JobName = JobNames.OutboxPublisher,
            JobId = null,
            Status = status,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            ErrorMessage = Truncate(errorMessage, 2000),
            TraceId = traceId
        });

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var messages = _outboxRepository.Query()
            .Where(entity =>
                (entity.Status == ReliableMessageStatus.Pending ||
                    entity.Status == ReliableMessageStatus.Processing) &&
                (!entity.NextRetryAt.HasValue || entity.NextRetryAt <= now))
            .OrderBy(entity => entity.CreatedAt)
            .Take(BatchSize)
            .ToList();

        foreach (var message in messages)
        {
            await PublishOneAsync(message, cancellationToken);
        }
    }

    private async Task PublishOneAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            message.Status = ReliableMessageStatus.Processing;
            message.ErrorMessage = null;
            message.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(5);
            _outboxRepository.Update(message);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _messageBus.PublishRawAsync(
                message.Exchange,
                message.RoutingKey,
                message.Payload,
                message.MessageType,
                message.Headers,
                cancellationToken);

            message.Status = ReliableMessageStatus.Published;
            message.ProcessedAt = DateTimeOffset.UtcNow;
            message.NextRetryAt = null;
            message.ErrorMessage = null;
            _outboxRepository.Update(message);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Outbox message published. MessageId: {MessageId}, TraceId: {TraceId}", message.MessageId, _traceContextAccessor.TraceId);
        }
        catch (Exception exception)
        {
            message.RetryCount++;
            message.Status = message.RetryCount >= MaxRetryCount
                ? ReliableMessageStatus.Failed
                : ReliableMessageStatus.Pending;
            message.ErrorMessage = Truncate(exception.Message, 2000);
            message.NextRetryAt = message.Status == ReliableMessageStatus.Failed
                ? null
                : DateTimeOffset.UtcNow.Add(GetRetryDelay(message.RetryCount));
            _outboxRepository.Update(message);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);

            _logger.LogWarning(
                exception,
                "Outbox message publish failed. MessageId: {MessageId}, RetryCount: {RetryCount}",
                message.MessageId,
                message.RetryCount);
        }
    }

    private static TimeSpan GetRetryDelay(int retryCount)
    {
        var seconds = Math.Min(300, Math.Pow(2, Math.Max(1, retryCount)) * 5);
        return TimeSpan.FromSeconds(seconds);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
    }

    private string EnsureTraceId()
    {
        if (!string.IsNullOrWhiteSpace(_traceContextAccessor.TraceId))
        {
            return _traceContextAccessor.TraceId;
        }

        var traceId = ActivityTraceId.CreateRandom().ToString();
        _traceContextAccessor.TraceId = traceId;
        return traceId;
    }

    private static Activity? StartJobActivity(string traceId, string name)
    {
        var activity = ActivitySource.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("app.trace_id", traceId);
        activity?.SetTag("job.system", "hangfire");
        return activity;
    }
}
