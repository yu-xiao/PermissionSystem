using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Messaging;

public sealed class OutboxService : IOutboxService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<OutboxMessage> _outboxRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITraceContextAccessor _traceContextAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAsyncQueryExecutor _asyncQueryExecutor;

    public OutboxService(
        IRepository<OutboxMessage> outboxRepository,
        ICurrentUserService currentUserService,
        ITraceContextAccessor traceContextAccessor,
        IUnitOfWork unitOfWork,
        IAsyncQueryExecutor asyncQueryExecutor)
    {
        _outboxRepository = outboxRepository;
        _currentUserService = currentUserService;
        _traceContextAccessor = traceContextAccessor;
        _unitOfWork = unitOfWork;
        _asyncQueryExecutor = asyncQueryExecutor;
    }

    public async Task<string> EnqueueAsync(
        CreateOutboxMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var messageId = string.IsNullOrWhiteSpace(request.MessageId)
            ? Guid.NewGuid().ToString("N")
            : request.MessageId.Trim();

        var entity = new OutboxMessage
        {
            TenantId = ResolveTenantId(request.TenantId),
            MessageId = messageId,
            Exchange = TrimRequired(request.Exchange, "Exchange is required."),
            RoutingKey = TrimRequired(request.RoutingKey, "Routing key is required."),
            MessageType = TrimRequired(request.MessageType, "Message type is required."),
            Payload = TrimRequired(request.Payload, "Payload is required."),
            Headers = MergeTraceHeaders(request.Headers),
            Status = ReliableMessageStatus.Pending,
            RetryCount = 0,
            NextRetryAt = DateTimeOffset.UtcNow
        };

        await _outboxRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return messageId;
    }

    public Task<string> EnqueueAsync<TMessage>(
        string exchange,
        string routingKey,
        TMessage message,
        IReadOnlyDictionary<string, string>? headers = null,
        Guid? tenantId = null,
        string? messageId = null,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(message, JsonOptions);
        var headerJson = headers is null ? null : JsonSerializer.Serialize(headers, JsonOptions);
        var messageType = typeof(TMessage).FullName ?? typeof(TMessage).Name;

        return EnqueueAsync(
            new CreateOutboxMessageRequest
            {
                TenantId = tenantId,
                Exchange = exchange,
                RoutingKey = routingKey,
                MessageType = messageType,
                Payload = payload,
                Headers = headerJson,
                MessageId = messageId
            },
            cancellationToken);
    }

    public async Task<PagedResult<OutboxMessageResponse>> GetPagedAsync(
        OutboxMessageQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyQuery(_outboxRepository.Query(), request);
        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var rows = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderByDescending(entity => entity.CreatedAt)
                .Skip(request.Skip)
                .Take(request.PageSize)
                .Select(entity => new
                {
                    Id = entity.Id,
                    TenantId = entity.TenantId,
                    MessageId = entity.MessageId,
                    Exchange = entity.Exchange,
                    RoutingKey = entity.RoutingKey,
                    MessageType = entity.MessageType,
                    entity.Status,
                    RetryCount = entity.RetryCount,
                    NextRetryAt = entity.NextRetryAt,
                    ErrorMessage = entity.ErrorMessage,
                    CreatedAt = entity.CreatedAt,
                    ProcessedAt = entity.ProcessedAt
                }),
            cancellationToken);
        var items = rows.Select(entity => new OutboxMessageResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            MessageId = entity.MessageId,
            Exchange = entity.Exchange,
            RoutingKey = entity.RoutingKey,
            MessageType = entity.MessageType,
            Status = entity.Status.ToString(),
            RetryCount = entity.RetryCount,
            NextRetryAt = entity.NextRetryAt,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt,
            ProcessedAt = entity.ProcessedAt
        }).ToList();

        return PagedResult<OutboxMessageResponse>.Create(items, request.PageIndex, request.PageSize, totalCount);
    }

    public async Task<OutboxMessageDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _outboxRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Outbox message was not found.");

        var tenantId = ResolveQueryTenantId(null);
        if (tenantId.HasValue && entity.TenantId != tenantId.Value)
        {
            throw new BusinessException(ErrorCode.NotFound, "Outbox message was not found.");
        }

        return ToDetailResponse(entity);
    }

    private IQueryable<OutboxMessage> ApplyQuery(IQueryable<OutboxMessage> query, OutboxMessageQueryRequest request)
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
                entity.Exchange.Contains(keyword) ||
                entity.RoutingKey.Contains(keyword) ||
                entity.MessageType.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(entity => entity.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.MessageType))
        {
            var messageType = request.MessageType.Trim();
            query = query.Where(entity => entity.MessageType.Contains(messageType));
        }

        if (!string.IsNullOrWhiteSpace(request.RoutingKey))
        {
            var routingKey = request.RoutingKey.Trim();
            query = query.Where(entity => entity.RoutingKey.Contains(routingKey));
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

    private Guid ResolveTenantId(Guid? requestedTenantId)
    {
        return requestedTenantId
            ?? _currentUserService.TenantId
            ?? throw new BusinessException(ErrorCode.ValidationFailed, "TenantId is required.");
    }

    private string? MergeTraceHeaders(string? headers)
    {
        var traceId = _traceContextAccessor.TraceId;
        if (string.IsNullOrWhiteSpace(traceId))
        {
            return string.IsNullOrWhiteSpace(headers) ? null : headers.Trim();
        }

        Dictionary<string, string> values = [];
        if (!string.IsNullOrWhiteSpace(headers))
        {
            try
            {
                values = JsonSerializer.Deserialize<Dictionary<string, string>>(headers, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                values["raw-headers"] = headers.Trim();
            }
        }

        values["X-Trace-Id"] = traceId;
        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private Guid? ResolveQueryTenantId(Guid? requestedTenantId)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            return requestedTenantId;
        }

        return _currentUserService.TenantId ?? requestedTenantId;
    }

    private static OutboxMessageResponse ToResponse(OutboxMessage entity)
    {
        return new OutboxMessageResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            MessageId = entity.MessageId,
            Exchange = entity.Exchange,
            RoutingKey = entity.RoutingKey,
            MessageType = entity.MessageType,
            Headers = entity.Headers,
            Status = entity.Status,
            RetryCount = entity.RetryCount,
            NextRetryAt = entity.NextRetryAt,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt,
            ProcessedAt = entity.ProcessedAt
        };
    }

    private static OutboxMessageDetailResponse ToDetailResponse(OutboxMessage entity)
    {
        return new OutboxMessageDetailResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            MessageId = entity.MessageId,
            Exchange = entity.Exchange,
            RoutingKey = entity.RoutingKey,
            MessageType = entity.MessageType,
            Payload = entity.Payload,
            Headers = entity.Headers,
            Status = entity.Status,
            RetryCount = entity.RetryCount,
            NextRetryAt = entity.NextRetryAt,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt,
            ProcessedAt = entity.ProcessedAt
        };
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
