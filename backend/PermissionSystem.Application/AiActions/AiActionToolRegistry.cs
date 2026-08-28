using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiActions;

public sealed class AiActionToolRegistry : IAiActionToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAiBusinessActionHandler> _handlers;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAiCenterConfiguration _configuration;

    public AiActionToolRegistry(
        IEnumerable<IAiBusinessActionHandler> handlers,
        ICurrentUserService currentUserService,
        IAiCenterConfiguration configuration)
    {
        _handlers = handlers.ToDictionary(handler => handler.ToolDefinition.ToolCode, StringComparer.Ordinal);
        _currentUserService = currentUserService;
        _configuration = configuration;
    }

    public IReadOnlyList<AiToolDefinition> GetAvailableTools()
    {
        return HasAccess()
            ? _handlers.Values.Select(handler => handler.ToolDefinition).ToList()
            : [];
    }

    public bool IsActionTool(string toolCode) => _handlers.ContainsKey(toolCode);

    public Task<AiActionToolExecutionResult> ExecuteAsync(
        string toolCode,
        AiActionDraftContext context,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        if (!HasAccess() || !_handlers.TryGetValue(toolCode, out var handler))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The requested AI action is not available.");
        }

        return handler.PrepareDraftAsync(context, argumentsJson, cancellationToken);
    }

    private bool HasAccess()
    {
        return _configuration.Enabled &&
            _currentUserService.IsAuthenticated &&
            _currentUserService.UserId.HasValue &&
            _currentUserService.TenantId is { } tenantId &&
            _configuration.AllowedTenantIds.Contains(tenantId) &&
            _currentUserService.HasPermission(AiCenterConstants.DocumentDraftPermission) &&
            _currentUserService.HasPermission("demo-business-order:create");
    }
}

internal sealed class NullAiActionToolRegistry : IAiActionToolRegistry
{
    public IReadOnlyList<AiToolDefinition> GetAvailableTools() => [];

    public bool IsActionTool(string toolCode) => false;

    public Task<AiActionToolExecutionResult> ExecuteAsync(
        string toolCode,
        AiActionDraftContext context,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        throw new BusinessException(ErrorCode.Forbidden, "AI actions are not available.");
    }
}

internal sealed class NullAiDocumentDraftReader : IAiDocumentDraftReader
{
    public Task<IReadOnlyList<AiDocumentDraftResponse>> GetByConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiDocumentDraftResponse>>([]);

    public Task<IReadOnlyList<AiDocumentDraftResponse>> GetByRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiDocumentDraftResponse>>([]);
}
