using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Messaging;

public sealed class InboxMessageQueryRequest : PaginationRequest
{
    public Guid? TenantId { get; init; }

    public string? Keyword { get; init; }

    public string? Consumer { get; init; }

    public string? Status { get; init; }

    public string? MessageType { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }
}

public sealed class InboxConsumeRequest
{
    public Guid? TenantId { get; init; }

    public string MessageId { get; init; } = string.Empty;

    public string Consumer { get; init; } = string.Empty;

    public string MessageType { get; init; } = string.Empty;

    public string Payload { get; init; } = string.Empty;
}

public class InboxMessageResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string MessageId { get; init; } = string.Empty;

    public string Consumer { get; init; } = string.Empty;

    public string MessageType { get; init; } = string.Empty;

    public string PayloadHash { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; init; }
}

public sealed class InboxMessageDetailResponse : InboxMessageResponse;

public interface IInboxService
{
    Task<PagedResult<InboxMessageResponse>> GetPagedAsync(
        InboxMessageQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<InboxMessageDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> HasProcessedAsync(
        string messageId,
        string consumer,
        CancellationToken cancellationToken = default);

    Task<bool> TryBeginProcessAsync(
        InboxConsumeRequest request,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        string messageId,
        string consumer,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        string messageId,
        string consumer,
        CancellationToken cancellationToken = default);

    Task<bool> ExecuteOnceAsync(
        InboxConsumeRequest request,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);
}
