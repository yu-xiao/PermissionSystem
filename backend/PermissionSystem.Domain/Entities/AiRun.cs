using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiRun : BaseEntity
{
    public string? ExecutionMode { get; set; }
    public string? ActorSessionId { get; set; }
    public Guid? ActorSecurityStamp { get; set; }
    public DateTimeOffset? QueueDeadlineAt { get; set; }
    public string? SubmissionHash { get; set; }
    public string? RequestHash { get; set; }
    public long? ProgressVersion { get; set; }

    public Guid? ScenarioId { get; set; }

    public Guid? ScenarioVersionId { get; set; }

    public Guid ConversationId { get; set; }

    public Guid RequestMessageId { get; set; }

    public Guid? ResponseMessageId { get; set; }

    public Guid ProviderConfigId { get; set; }

    public Guid? FinalProviderConfigId { get; set; }

    public Guid ActorUserId { get; set; }

    public string? ScenarioContentHash { get; set; }

    public string? BuildIdentity { get; set; }

    public string? ExecutionConfigurationJson { get; set; }

    public string? ExecutionConfigurationHash { get; set; }

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
