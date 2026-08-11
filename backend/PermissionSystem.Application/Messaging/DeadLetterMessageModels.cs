using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Messaging;

public sealed class DeadLetterMessageQueryRequest : PaginationRequest
{
    public Guid? TenantId { get; init; }

    public string? Keyword { get; init; }

    public string? Consumer { get; init; }

    public string? SourceQueue { get; init; }

    public string? Status { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }
}

public sealed class RecordDeadLetterMessageRequest
{
    public Guid TenantId { get; init; }

    public string MessageId { get; init; } = string.Empty;

    public string Consumer { get; init; } = string.Empty;

    public string SourceQueue { get; init; } = string.Empty;

    public string Exchange { get; init; } = string.Empty;

    public string RoutingKey { get; init; } = string.Empty;

    public string MessageType { get; init; } = string.Empty;

    public string Payload { get; init; } = string.Empty;

    public string? Headers { get; init; }

    public int RetryCount { get; init; }

    public string FailureReason { get; init; } = string.Empty;
}

public sealed class DiscardDeadLetterMessageRequest
{
    public string Remark { get; init; } = string.Empty;
}

public class DeadLetterMessageResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string MessageId { get; init; } = string.Empty;

    public string Consumer { get; init; } = string.Empty;

    public string SourceQueue { get; init; } = string.Empty;

    public string Exchange { get; init; } = string.Empty;

    public string RoutingKey { get; init; } = string.Empty;

    public string MessageType { get; init; } = string.Empty;

    public int RetryCount { get; init; }

    public string FailureReason { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int ReplayCount { get; init; }

    public DateTimeOffset? LastReplayedAt { get; init; }

    public string? DispositionRemark { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class DeadLetterMessageDetailResponse : DeadLetterMessageResponse
{
    public string Payload { get; init; } = string.Empty;

    public string? Headers { get; init; }
}

public interface IDeadLetterMessageService
{
    Task RecordAsync(RecordDeadLetterMessageRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<DeadLetterMessageResponse>> GetPagedAsync(
        DeadLetterMessageQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<DeadLetterMessageDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReplayAsync(Guid id, CancellationToken cancellationToken = default);

    Task DiscardAsync(Guid id, DiscardDeadLetterMessageRequest request, CancellationToken cancellationToken = default);
}
