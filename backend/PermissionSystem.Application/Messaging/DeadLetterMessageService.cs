using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Messaging;

public sealed class DeadLetterMessageService : IDeadLetterMessageService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<DeadLetterMessage> _deadLetterRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAsyncQueryExecutor _asyncQueryExecutor;
    private readonly IMessageBus _messageBus;
    private readonly IUnitOfWork _unitOfWork;

    public DeadLetterMessageService(
        IRepository<DeadLetterMessage> deadLetterRepository,
        ICurrentUserService currentUserService,
        IAsyncQueryExecutor asyncQueryExecutor,
        IMessageBus messageBus,
        IUnitOfWork unitOfWork)
    {
        _deadLetterRepository = deadLetterRepository;
        _currentUserService = currentUserService;
        _asyncQueryExecutor = asyncQueryExecutor;
        _messageBus = messageBus;
        _unitOfWork = unitOfWork;
    }

    public async Task RecordAsync(RecordDeadLetterMessageRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId(request.TenantId);
        var messageId = TrimRequired(request.MessageId, "MessageId is required.");
        var consumer = TrimRequired(request.Consumer, "Consumer is required.");
        var existing = await _asyncQueryExecutor.FirstOrDefaultAsync(
            _deadLetterRepository.Query().Where(entity =>
                entity.TenantId == tenantId && entity.MessageId == messageId && entity.Consumer == consumer),
            cancellationToken);

        if (existing is null)
        {
            await _deadLetterRepository.AddAsync(new DeadLetterMessage
            {
                TenantId = tenantId,
                MessageId = messageId,
                Consumer = consumer,
                SourceQueue = TrimRequired(request.SourceQueue, "SourceQueue is required."),
                Exchange = TrimRequired(request.Exchange, "Exchange is required."),
                RoutingKey = TrimRequired(request.RoutingKey, "RoutingKey is required."),
                MessageType = TrimRequired(request.MessageType, "MessageType is required."),
                Payload = request.Payload,
                Headers = request.Headers,
                RetryCount = Math.Max(0, request.RetryCount),
                FailureReason = TrimRequired(request.FailureReason, "FailureReason is required."),
                Status = DeadLetterMessageStatuses.Pending
            }, cancellationToken);
        }
        else
        {
            existing.SourceQueue = TrimRequired(request.SourceQueue, "SourceQueue is required.");
            existing.Exchange = TrimRequired(request.Exchange, "Exchange is required.");
            existing.RoutingKey = TrimRequired(request.RoutingKey, "RoutingKey is required.");
            existing.MessageType = TrimRequired(request.MessageType, "MessageType is required.");
            existing.Payload = request.Payload;
            existing.Headers = request.Headers;
            existing.RetryCount = Math.Max(0, request.RetryCount);
            existing.FailureReason = TrimRequired(request.FailureReason, "FailureReason is required.");
            existing.Status = DeadLetterMessageStatuses.Pending;
            existing.DispositionRemark = null;
            _deadLetterRepository.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<DeadLetterMessageResponse>> GetPagedAsync(
        DeadLetterMessageQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyQuery(_deadLetterRepository.Query(), request);
        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var rows = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderByDescending(entity => entity.CreatedAt)
                .Skip(request.Skip)
                .Take(request.PageSize)
                .Select(entity => new DeadLetterMessageResponse
                {
                    Id = entity.Id,
                    TenantId = entity.TenantId,
                    MessageId = entity.MessageId,
                    Consumer = entity.Consumer,
                    SourceQueue = entity.SourceQueue,
                    Exchange = entity.Exchange,
                    RoutingKey = entity.RoutingKey,
                    MessageType = entity.MessageType,
                    RetryCount = entity.RetryCount,
                    FailureReason = entity.FailureReason,
                    Status = entity.Status,
                    ReplayCount = entity.ReplayCount,
                    LastReplayedAt = entity.LastReplayedAt,
                    DispositionRemark = entity.DispositionRemark,
                    CreatedAt = entity.CreatedAt
                }),
            cancellationToken);

        return PagedResult<DeadLetterMessageResponse>.Create(rows, request.PageIndex, request.PageSize, totalCount);
    }

    public async Task<DeadLetterMessageDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        return ToDetailResponse(entity);
    }

    public async Task ReplayAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity.Status != DeadLetterMessageStatuses.Pending)
        {
            throw new BusinessException(ErrorCode.Conflict, "Only pending dead-letter messages can be replayed.");
        }

        if (!_messageBus.IsEnabled)
        {
            throw new BusinessException(ErrorCode.Conflict, "RabbitMQ is disabled; the dead-letter message cannot be replayed.");
        }

        await _messageBus.PublishRawAsync(
            entity.Exchange,
            entity.RoutingKey,
            entity.Payload,
            entity.MessageType,
            BuildReplayHeaders(entity.Headers),
            entity.MessageId,
            entity.TenantId,
            cancellationToken);

        entity.Status = DeadLetterMessageStatuses.Replayed;
        entity.ReplayCount++;
        entity.LastReplayedAt = DateTimeOffset.UtcNow;
        entity.DispositionRemark = null;
        _deadLetterRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DiscardAsync(
        Guid id,
        DiscardDeadLetterMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity.Status != DeadLetterMessageStatuses.Pending)
        {
            throw new BusinessException(ErrorCode.Conflict, "Only pending dead-letter messages can be discarded.");
        }

        entity.Status = DeadLetterMessageStatuses.Discarded;
        entity.DispositionRemark = TrimRequired(request.Remark, "Discard remark is required.");
        _deadLetterRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<DeadLetterMessage> GetEntityAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _deadLetterRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Dead-letter message was not found.");
        var tenantId = ResolveQueryTenantId(null);
        if (tenantId.HasValue && entity.TenantId != tenantId.Value)
        {
            throw new BusinessException(ErrorCode.NotFound, "Dead-letter message was not found.");
        }

        return entity;
    }

    private IQueryable<DeadLetterMessage> ApplyQuery(
        IQueryable<DeadLetterMessage> query,
        DeadLetterMessageQueryRequest request)
    {
        var tenantId = ResolveQueryTenantId(request.TenantId);
        if (tenantId.HasValue)
        {
            query = query.Where(entity => entity.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.MessageId.Contains(keyword) ||
                entity.MessageType.Contains(keyword) ||
                entity.FailureReason.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.Consumer))
        {
            var consumer = request.Consumer.Trim();
            query = query.Where(entity => entity.Consumer.Contains(consumer));
        }

        if (!string.IsNullOrWhiteSpace(request.SourceQueue))
        {
            var sourceQueue = request.SourceQueue.Trim();
            query = query.Where(entity => entity.SourceQueue.Contains(sourceQueue));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(entity => entity.Status == status);
        }

        if (request.StartTime.HasValue)
        {
            query = query.Where(entity => entity.CreatedAt >= request.StartTime.Value);
        }

        if (request.EndTime.HasValue)
        {
            query = query.Where(entity => entity.CreatedAt <= request.EndTime.Value);
        }

        return query;
    }

    private Guid? ResolveQueryTenantId(Guid? requestedTenantId)
    {
        return _currentUserService.IsSuperAdmin
            ? requestedTenantId
            : _currentUserService.TenantId ?? requestedTenantId;
    }

    private static string BuildReplayHeaders(string? headers)
    {
        Dictionary<string, string> values = [];
        if (!string.IsNullOrWhiteSpace(headers))
        {
            values = JsonSerializer.Deserialize<Dictionary<string, string>>(headers, JsonOptions) ?? [];
        }

        values.Remove("X-Consumer-Retry-Count");
        values.Remove("X-Dead-Letter-Reason");
        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static DeadLetterMessageDetailResponse ToDetailResponse(DeadLetterMessage entity)
    {
        return new DeadLetterMessageDetailResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            MessageId = entity.MessageId,
            Consumer = entity.Consumer,
            SourceQueue = entity.SourceQueue,
            Exchange = entity.Exchange,
            RoutingKey = entity.RoutingKey,
            MessageType = entity.MessageType,
            Payload = entity.Payload,
            Headers = entity.Headers,
            RetryCount = entity.RetryCount,
            FailureReason = entity.FailureReason,
            Status = entity.Status,
            ReplayCount = entity.ReplayCount,
            LastReplayedAt = entity.LastReplayedAt,
            DispositionRemark = entity.DispositionRemark,
            CreatedAt = entity.CreatedAt
        };
    }

    private static Guid RequireTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "TenantId is required.");
        }

        return tenantId;
    }

    private static string TrimRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }
}
