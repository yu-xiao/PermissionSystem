using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Messaging;

public sealed class OutboxMessageQueryRequest : PaginationRequest
{
    public Guid? TenantId { get; init; }

    public string? Keyword { get; init; }

    public string? Status { get; init; }

    public string? MessageType { get; init; }

    public string? RoutingKey { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }
}

public sealed class CreateOutboxMessageRequest
{
    public Guid? TenantId { get; init; }

    public string Exchange { get; init; } = string.Empty;

    public string RoutingKey { get; init; } = string.Empty;

    public string MessageType { get; init; } = string.Empty;

    public string Payload { get; init; } = string.Empty;

    public string? Headers { get; init; }

    public string? MessageId { get; init; }
}

public class OutboxMessageResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string MessageId { get; init; } = string.Empty;

    public string Exchange { get; init; } = string.Empty;

    public string RoutingKey { get; init; } = string.Empty;

    public string MessageType { get; init; } = string.Empty;

    public string? Headers { get; init; }

    public string Status { get; init; } = string.Empty;

    public int RetryCount { get; init; }

    public DateTimeOffset? NextRetryAt { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; init; }
}

public sealed class OutboxMessageDetailResponse : OutboxMessageResponse
{
    public string Payload { get; init; } = string.Empty;
}

public interface IOutboxService
{
    Task<string> EnqueueAsync(
        CreateOutboxMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<string> EnqueueAsync<TMessage>(
        string exchange,
        string routingKey,
        TMessage message,
        IReadOnlyDictionary<string, string>? headers = null,
        Guid? tenantId = null,
        string? messageId = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OutboxMessageResponse>> GetPagedAsync(
        OutboxMessageQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<OutboxMessageDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
