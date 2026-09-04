using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiTools;

public sealed class AiReadOnlyToolRegistry : IAiReadOnlyToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAiReadOnlyToolHandler> _handlers;
    private readonly IReadOnlyList<IAiReadOnlyToolHandler> _orderedHandlers;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantContext _tenantContext;
    private readonly ITraceContextAccessor _traceContextAccessor;

    public AiReadOnlyToolRegistry(
        IEnumerable<IAiReadOnlyToolHandler> handlers,
        ICurrentUserService currentUserService,
        ITenantContext tenantContext,
        ITraceContextAccessor traceContextAccessor)
    {
        var registered = handlers.ToArray();
        EnsureUnique(registered, handler => handler.Definition.ToolCode, "tool code");
        EnsureUnique(registered, handler => handler.Definition.FunctionName, "function name");
        foreach (var handler in registered)
        {
            ValidateDefinition(handler.Definition);
        }

        _handlers = registered.ToDictionary(
            handler => handler.Definition.ToolCode,
            StringComparer.Ordinal);
        _orderedHandlers = registered;
        _currentUserService = currentUserService;
        _tenantContext = tenantContext;
        _traceContextAccessor = traceContextAccessor;
    }

    public IReadOnlyList<AiToolDefinition> GetAvailableTools()
    {
        if (!TryGetExecutionContext(out _))
        {
            return [];
        }

        return _orderedHandlers
            .Where(IsAvailable)
            .Select(handler => handler.Definition)
            .ToList();
    }

    public async Task<AiToolExecutionResult> ExecuteAsync(
        string toolCode,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = GetRequiredExecutionContext();
        if (!_handlers.TryGetValue(toolCode, out var handler) || !IsAvailable(handler))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The requested AI tool is not available.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(handler.Definition.TimeoutSeconds));
        try
        {
            return await handler.ExecuteAsync(context, argumentsJson, timeoutSource.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException(
                ErrorCode.BusinessError,
                "The AI tool execution timed out.",
                exception);
        }
    }

    private bool IsAvailable(IAiReadOnlyToolHandler handler)
    {
        return handler.IsEnabled &&
            handler.Definition.RequiredPermissions.All(_currentUserService.HasPermission);
    }

    private AiToolExecutionContext GetRequiredExecutionContext()
    {
        if (!_currentUserService.IsAuthenticated)
        {
            throw new BusinessException(ErrorCode.Unauthorized, "Authentication is required.");
        }

        if (!TryGetExecutionContext(out var context))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The AI tool tenant context is invalid.");
        }

        return context!;
    }

    private bool TryGetExecutionContext(out AiToolExecutionContext? context)
    {
        context = null;
        if (!_currentUserService.IsAuthenticated ||
            !_currentUserService.UserId.HasValue ||
            !_currentUserService.TenantId.HasValue ||
            !_tenantContext.TenantId.HasValue ||
            _currentUserService.TenantId.Value != _tenantContext.TenantId.Value)
        {
            return false;
        }

        context = new AiToolExecutionContext
        {
            ActorUserId = _currentUserService.UserId.Value,
            TenantId = _currentUserService.TenantId.Value,
            TraceId = _traceContextAccessor.TraceId
        };
        return true;
    }

    private static void EnsureUnique(
        IReadOnlyCollection<IAiReadOnlyToolHandler> handlers,
        Func<IAiReadOnlyToolHandler, string> keySelector,
        string keyName)
    {
        var duplicate = handlers
            .GroupBy(keySelector, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate AI read-only tool {keyName} '{duplicate.Key}'.");
        }
    }

    private static void ValidateDefinition(AiToolDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ToolCode) ||
            string.IsNullOrWhiteSpace(definition.FunctionName) ||
            definition.FunctionName.Length > 64 ||
            definition.FunctionName.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-') ||
            string.IsNullOrWhiteSpace(definition.Version) ||
            string.IsNullOrWhiteSpace(definition.Description) ||
            !IsJsonObject(definition.InputSchemaJson) ||
            !IsJsonObject(definition.OutputSchemaJson) ||
            string.IsNullOrWhiteSpace(definition.DataClassification) ||
            string.IsNullOrWhiteSpace(definition.DataScopePolicy) ||
            definition.RequiredPermissions.Count == 0 ||
            definition.RequiredPermissions.Any(string.IsNullOrWhiteSpace) ||
            definition.TimeoutSeconds is < 1 or > 90 ||
            definition.MaxRows is <= 0)
        {
            throw new InvalidOperationException(
                $"AI read-only tool definition '{definition.ToolCode}' is invalid.");
        }
    }

    private static bool IsJsonObject(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
