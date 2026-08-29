using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Pagination;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiConversationQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }
}

public sealed class CreateAiConversationRequest
{
    public string? Title { get; init; }
}

public sealed class SendAiMessageRequest
{
    public string Content { get; init; } = string.Empty;
}

public class AiConversationListResponse
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public AiConversationStatus Status { get; init; }

    public DateTimeOffset LastMessageAt { get; init; }

    public DateTimeOffset? LastRunAt { get; init; }
}

public sealed class AiMessageResponse
{
    public Guid Id { get; init; }

    public AiMessageRole Role { get; init; }

    public string Content { get; init; } = string.Empty;

    public int Sequence { get; init; }

    public bool ModelGenerated { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public Guid? RunId { get; init; }

    public AiFeedbackResponse? Feedback { get; init; }
}

public sealed class AiConversationDetailResponse : AiConversationListResponse
{
    public string AgentCode { get; init; } = string.Empty;

    public string AgentVersion { get; init; } = string.Empty;

    public IReadOnlyList<AiMessageResponse> Messages { get; init; } = [];

    public IReadOnlyList<AiDocumentDraftResponse> DocumentDrafts { get; init; } = [];
}

public sealed class AiRunResponse
{
    public Guid Id { get; init; }

    public Guid ConversationId { get; init; }

    public Guid RequestMessageId { get; init; }

    public Guid? ResponseMessageId { get; init; }

    public AiRunStatus Status { get; init; }

    public string ModelName { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public long? DurationMilliseconds { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public decimal? EstimatedCost { get; init; }

    public int FallbackCount { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorSummary { get; init; }

    public DateTimeOffset? CancellationRequestedAt { get; init; }

    public AiMessageResponse? ResponseMessage { get; init; }

    public IReadOnlyList<AiToolCitation> Citations { get; init; } = [];

    public IReadOnlyList<AiDocumentDraftResponse> DocumentDrafts { get; init; } = [];
}

public sealed class AiRunRealtimeMessage
{
    public Guid RunId { get; init; }

    public Guid ConversationId { get; init; }

    public string EventType { get; init; } = string.Empty;

    public AiRunStatus Status { get; init; }

    public string? ToolCode { get; init; }

    public AiInvocationStatus? ToolStatus { get; init; }

    public string? ErrorCode { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}

public interface IAiRunRealtimeSender
{
    Task SendToUserAsync(
        Guid userId,
        AiRunRealtimeMessage message,
        CancellationToken cancellationToken = default);
}

public interface IAiRunCancellationProbe
{
    Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken = default);
}

public interface IAiConversationService
{
    Task<PermissionSystem.Shared.Results.PagedResult<AiConversationListResponse>> GetPagedAsync(
        AiConversationQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<AiConversationDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AiConversationDetailResponse> CreateAsync(
        CreateAiConversationRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AiRunResponse> SendMessageAsync(
        Guid conversationId,
        SendAiMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<AiRunResponse> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiToolCitation>> GetCitationsAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task CancelRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<AiRunResponse> RetryRunAsync(Guid runId, CancellationToken cancellationToken = default);
}
