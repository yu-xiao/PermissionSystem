using System.Security.Cryptography;
using System.Text;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Messaging;

public sealed class InboxService : IInboxService
{
    private readonly IRepository<InboxMessage> _inboxRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAsyncQueryExecutor _asyncQueryExecutor;

    public InboxService(
        IRepository<InboxMessage> inboxRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IAsyncQueryExecutor asyncQueryExecutor)
    {
        _inboxRepository = inboxRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _asyncQueryExecutor = asyncQueryExecutor;
    }

    public async Task<PagedResult<InboxMessageResponse>> GetPagedAsync(
        InboxMessageQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyQuery(_inboxRepository.Query(), request);
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
                    Consumer = entity.Consumer,
                    MessageType = entity.MessageType,
                    PayloadHash = entity.PayloadHash,
                    entity.Status,
                    entity.ErrorMessage,
                    CreatedAt = entity.CreatedAt,
                    ProcessedAt = entity.ProcessedAt
                }),
            cancellationToken);
        var items = rows.Select(entity => new InboxMessageResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            MessageId = entity.MessageId,
            Consumer = entity.Consumer,
            MessageType = entity.MessageType,
            PayloadHash = entity.PayloadHash,
            Status = entity.Status.ToString(),
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt,
            ProcessedAt = entity.ProcessedAt
        }).ToList();

        return PagedResult<InboxMessageResponse>.Create(items, request.PageIndex, request.PageSize, totalCount);
    }

    public async Task<InboxMessageDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _inboxRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Inbox message was not found.");

        var tenantId = ResolveQueryTenantId(null);
        if (tenantId.HasValue && entity.TenantId != tenantId.Value)
        {
            throw new BusinessException(ErrorCode.NotFound, "Inbox message was not found.");
        }

        return ToDetailResponse(entity);
    }

    public Task<bool> HasProcessedAsync(
        string messageId,
        string consumer,
        CancellationToken cancellationToken = default)
    {
        var normalizedMessageId = TrimRequired(messageId, "MessageId is required.");
        var normalizedConsumer = TrimRequired(consumer, "Consumer is required.");

        return _asyncQueryExecutor.AnyAsync(
            _inboxRepository.Query().Where(entity =>
                entity.MessageId == normalizedMessageId &&
                entity.Consumer == normalizedConsumer &&
                entity.Status == ReliableMessageStatus.Processed),
            cancellationToken);
    }

    public async Task<bool> TryBeginProcessAsync(
        InboxConsumeRequest request,
        CancellationToken cancellationToken = default)
    {
        var messageId = TrimRequired(request.MessageId, "MessageId is required.");
        var consumer = TrimRequired(request.Consumer, "Consumer is required.");
        var existing = await _asyncQueryExecutor.FirstOrDefaultAsync(
            _inboxRepository.Query().Where(entity =>
                entity.MessageId == messageId && entity.Consumer == consumer),
            cancellationToken);

        if (existing is not null)
        {
            return existing.Status != ReliableMessageStatus.Processed && existing.Status != ReliableMessageStatus.Processing
                ? await ResetForRetryAsync(existing, request, cancellationToken)
                : false;
        }

        await _inboxRepository.AddAsync(
            new InboxMessage
            {
                TenantId = ResolveTenantId(request.TenantId),
                MessageId = messageId,
                Consumer = consumer,
                MessageType = TrimRequired(request.MessageType, "Message type is required."),
                PayloadHash = HashPayload(request.Payload),
                Status = ReliableMessageStatus.Processing
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task CompleteAsync(
        string messageId,
        string consumer,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetByMessageIdAndConsumerAsync(messageId, consumer, cancellationToken);
        entity.Status = ReliableMessageStatus.Processed;
        entity.ProcessedAt = DateTimeOffset.UtcNow;
        _inboxRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        string messageId,
        string consumer,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetByMessageIdAndConsumerAsync(messageId, consumer, cancellationToken);
        entity.Status = ReliableMessageStatus.Failed;
        entity.ErrorMessage = Truncate(errorMessage, 2000);
        _inboxRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExecuteOnceAsync(
        InboxConsumeRequest request,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var shouldProcess = await TryBeginProcessAsync(request, cancellationToken);
        if (!shouldProcess)
        {
            return false;
        }

        try
        {
            await handler(cancellationToken);
            await CompleteAsync(request.MessageId, request.Consumer, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(request.MessageId, request.Consumer, exception.Message, CancellationToken.None);
            throw;
        }
    }

    private async Task<bool> ResetForRetryAsync(
        InboxMessage entity,
        InboxConsumeRequest request,
        CancellationToken cancellationToken)
    {
        entity.MessageType = TrimRequired(request.MessageType, "Message type is required.");
        entity.PayloadHash = HashPayload(request.Payload);
        entity.Status = ReliableMessageStatus.Processing;
        entity.ProcessedAt = null;
        _inboxRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<InboxMessage> GetByMessageIdAndConsumerAsync(
        string messageId,
        string consumer,
        CancellationToken cancellationToken)
    {
        var normalizedMessageId = TrimRequired(messageId, "MessageId is required.");
        var normalizedConsumer = TrimRequired(consumer, "Consumer is required.");

        return await _asyncQueryExecutor.FirstOrDefaultAsync(
                _inboxRepository.Query().Where(entity =>
                    entity.MessageId == normalizedMessageId &&
                    entity.Consumer == normalizedConsumer),
                cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Inbox message was not found.");
    }

    private IQueryable<InboxMessage> ApplyQuery(IQueryable<InboxMessage> query, InboxMessageQueryRequest request)
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
                entity.Consumer.Contains(keyword) ||
                entity.MessageType.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.Consumer))
        {
            var consumer = request.Consumer.Trim();
            query = query.Where(entity => entity.Consumer.Contains(consumer));
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

    private Guid? ResolveQueryTenantId(Guid? requestedTenantId)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            return requestedTenantId;
        }

        return _currentUserService.TenantId ?? requestedTenantId;
    }

    private static InboxMessageResponse ToResponse(InboxMessage entity)
    {
        return new InboxMessageResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            MessageId = entity.MessageId,
            Consumer = entity.Consumer,
            MessageType = entity.MessageType,
            PayloadHash = entity.PayloadHash,
            Status = entity.Status,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt,
            ProcessedAt = entity.ProcessedAt
        };
    }

    private static InboxMessageDetailResponse ToDetailResponse(InboxMessage entity)
    {
        return new InboxMessageDetailResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            MessageId = entity.MessageId,
            Consumer = entity.Consumer,
            MessageType = entity.MessageType,
            PayloadHash = entity.PayloadHash,
            Status = entity.Status,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt,
            ProcessedAt = entity.ProcessedAt
        };
    }

    private static string HashPayload(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string TrimRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
    }
}
