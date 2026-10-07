using PermissionSystem.Application.AiTools;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiActions;

public sealed class AiActionToolRegistry : IAiActionToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAiBusinessActionHandler> _handlers;
    private readonly IReadOnlyDictionary<(string BusinessType, string HandlerVersion), AiBusinessActionDefinition> _definitions;
    private readonly AiBusinessActionAccessPolicy _accessPolicy;

    public AiActionToolRegistry(
        IEnumerable<IAiBusinessActionHandler> handlers,
        AiBusinessActionAccessPolicy accessPolicy)
    {
        var registered = handlers.ToArray();
        foreach (var handler in registered)
        {
            AiBusinessActionAccessPolicy.ValidateDefinition(handler.Definition);
        }

        EnsureUnique(registered, handler => handler.Definition.ToolDefinition.ToolCode, "tool code");
        EnsureUnique(registered, handler => handler.Definition.ToolDefinition.FunctionName, "function name");
        if (registered.GroupBy(handler => (handler.Definition.BusinessType, handler.Definition.HandlerVersion))
            .Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Duplicate AI action business type and handler version.");
        }

        _definitions = registered.ToDictionary(handler => (handler.Definition.BusinessType, handler.Definition.HandlerVersion),
            handler => handler.Definition);
        _handlers = registered.ToDictionary(handler => handler.Definition.ToolDefinition.ToolCode, StringComparer.Ordinal);
        _accessPolicy = accessPolicy;
    }

    public IReadOnlyList<AiToolDefinition> GetAvailableTools()
    {
        return _handlers.Values.Where(handler => _accessPolicy.CanPrepare(handler.Definition))
            .Select(handler => handler.Definition.ToolDefinition).ToList();
    }

    public bool IsActionTool(string toolCode) => _handlers.ContainsKey(toolCode);

    public AiBusinessActionDefinition? FindDefinition(string businessType, string handlerVersion) =>
        _definitions.GetValueOrDefault((businessType, handlerVersion));

    public Task<AiActionToolExecutionResult> ExecuteAsync(
        string toolCode,
        AiActionDraftContext context,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_handlers.TryGetValue(toolCode, out var handler))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The requested AI action is not available.");
        }

        var identity = _accessPolicy.EnsurePrepare(handler.Definition);
        if (context.ActorUserId != identity.UserId || context.TenantId != identity.TenantId ||
            context.ConversationId == Guid.Empty || context.RunId == Guid.Empty ||
            string.IsNullOrWhiteSpace(context.InvocationId) || context.InvocationId.Length > 128)
        {
            throw new BusinessException(ErrorCode.Forbidden, "The AI action context is invalid.");
        }

        return handler.PrepareDraftAsync(context, argumentsJson, cancellationToken);
    }

    private static void EnsureUnique(IEnumerable<IAiBusinessActionHandler> handlers,
        Func<IAiBusinessActionHandler, string> key, string name)
    {
        if (handlers.GroupBy(key, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException($"Duplicate AI action {name}.");
        }
    }
}

internal sealed class NullAiActionToolRegistry : IAiActionToolRegistry
{
    public IReadOnlyList<AiToolDefinition> GetAvailableTools() => [];

    public bool IsActionTool(string toolCode) => false;

    public AiBusinessActionDefinition? FindDefinition(string businessType, string handlerVersion) => null;

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
