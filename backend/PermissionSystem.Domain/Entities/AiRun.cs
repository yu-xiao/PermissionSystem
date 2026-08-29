using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiRun : BaseEntity
{
    public Guid ConversationId { get; set; }

    public Guid RequestMessageId { get; set; }

    public Guid? ResponseMessageId { get; set; }

    public Guid ProviderConfigId { get; set; }

    public Guid? FinalProviderConfigId { get; set; }

    public Guid ActorUserId { get; set; }

    public string AgentCode { get; set; } = string.Empty;

    public string AgentVersion { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public AiRunStatus Status { get; set; } = AiRunStatus.Pending;

    public string TraceId { get; set; } = string.Empty;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long? DurationMilliseconds { get; set; }

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    public decimal? EstimatedCost { get; set; }

    public int FallbackCount { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorSummary { get; set; }

    public DateTimeOffset? CancellationRequestedAt { get; set; }

    public DateTimeOffset? DeadlineAt { get; set; }

    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public Guid ExecutionLeaseId { get; set; }

    public Guid? RetryOfRunId { get; set; }
}
