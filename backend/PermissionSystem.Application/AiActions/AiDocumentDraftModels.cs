using PermissionSystem.Application.AiTools;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.AiActions;

public static class AiBusinessActionConstants
{
    public const string DemoBusinessOrderToolCode = "business.demo_business_order.prepare_draft";
    public const string DemoBusinessOrderFunctionName = "prepare_demo_business_order_draft";
}

public sealed class AiActionDraftContext
{
    public Guid TenantId { get; init; }

    public Guid ActorUserId { get; init; }

    public Guid ConversationId { get; init; }

    public Guid RunId { get; init; }

    public string InvocationId { get; init; } = string.Empty;
}

public sealed class AiDraftValidationError
{
    public string Field { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<AiDraftAssociationCandidate> Candidates { get; init; } = [];
}

public sealed class AiDraftAssociationCandidate
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

public sealed class DemoBusinessOrderDraftPayload
{
    public string? Title { get; init; }

    public string? CustomerName { get; init; }

    public decimal? Amount { get; init; }

    public Guid? DepartmentId { get; init; }

    public string? DepartmentCode { get; init; }

    public string? DepartmentName { get; init; }

    public string? DepartmentReference { get; init; }
}

public class PrepareDemoBusinessOrderDraftRequest
{
    public string? Title { get; init; }

    public string? CustomerName { get; init; }

    public decimal? Amount { get; init; }

    public Guid? DepartmentId { get; init; }

    public string? DepartmentReference { get; init; }
}

public sealed class UpdateAiDocumentDraftRequest : PrepareDemoBusinessOrderDraftRequest
{
    public byte[]? ConcurrencyToken { get; init; }
}

public sealed class CancelAiDocumentDraftRequest
{
    public byte[]? ConcurrencyToken { get; init; }
}

public sealed class AiDocumentDraftResponse
{
    public Guid Id { get; init; }

    public Guid ConversationId { get; init; }

    public Guid RunId { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string HandlerVersion { get; init; } = string.Empty;

    public AiDocumentDraftStatus Status { get; init; }

    public int DraftVersion { get; init; }

    public DemoBusinessOrderDraftPayload Payload { get; init; } = new();

    public string PayloadHash { get; init; } = string.Empty;

    public IReadOnlyList<AiDraftValidationError> ValidationErrors { get; init; } = [];

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? LastValidatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class AiBusinessActionSchemaResponse
{
    public string BusinessType { get; init; } = string.Empty;

    public string HandlerVersion { get; init; } = string.Empty;

    public string InputSchemaJson { get; init; } = string.Empty;
}

public sealed class AiActionToolExecutionResult
{
    public string ContentJson { get; init; } = string.Empty;

    public AiDocumentDraftResponse Draft { get; init; } = new();
}

public interface IAiDraftConfiguration
{
    int DraftExpirationMinutes { get; }

    int DraftRetentionDays { get; }
}

public interface IAiBusinessActionHandler
{
    string BusinessType { get; }

    string HandlerVersion { get; }

    AiToolDefinition ToolDefinition { get; }

    Task<AiActionToolExecutionResult> PrepareDraftAsync(
        AiActionDraftContext context,
        string argumentsJson,
        CancellationToken cancellationToken = default);
}

public interface IAiActionToolRegistry
{
    IReadOnlyList<AiToolDefinition> GetAvailableTools();

    bool IsActionTool(string toolCode);

    Task<AiActionToolExecutionResult> ExecuteAsync(
        string toolCode,
        AiActionDraftContext context,
        string argumentsJson,
        CancellationToken cancellationToken = default);
}

public interface IAiDocumentDraftReader
{
    Task<IReadOnlyList<AiDocumentDraftResponse>> GetByConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiDocumentDraftResponse>> GetByRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}

public interface IAiDocumentDraftService : IAiDocumentDraftReader
{
    Task<AiBusinessActionSchemaResponse> GetDemoBusinessOrderSchemaAsync(
        CancellationToken cancellationToken = default);

    Task<AiDocumentDraftResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AiDocumentDraftResponse> UpdateAsync(
        Guid id,
        UpdateAiDocumentDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<AiDocumentDraftResponse> CancelAsync(
        Guid id,
        CancelAiDocumentDraftRequest request,
        CancellationToken cancellationToken = default);
}
