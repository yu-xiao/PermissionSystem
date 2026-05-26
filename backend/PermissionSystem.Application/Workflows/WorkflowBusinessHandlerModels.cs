using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Workflows;

public sealed class WorkflowBusinessContext
{
    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public string BusinessTitle { get; init; } = string.Empty;

    public Guid WorkflowInstanceId { get; init; }

    public Guid StarterUserId { get; init; }

    public string StarterUserName { get; init; } = string.Empty;

    public string? FormDataJson { get; init; }

    public WorkflowActionType Action { get; init; }

    public string? Comment { get; init; }
}

public interface IWorkflowBusinessHandler
{
    string BusinessType { get; }

    Task OnWorkflowStartedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken);

    Task OnWorkflowApprovedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken);

    Task OnWorkflowRejectedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken);

    Task OnWorkflowWithdrawnAsync(WorkflowBusinessContext context, CancellationToken cancellationToken);

    Task OnWorkflowCancelledAsync(WorkflowBusinessContext context, CancellationToken cancellationToken);
}

public interface IWorkflowBusinessHandlerResolver
{
    IWorkflowBusinessHandler Resolve(string businessType);
}

public sealed class WorkflowBusinessHandlerResolver : IWorkflowBusinessHandlerResolver
{
    private readonly IReadOnlyDictionary<string, IWorkflowBusinessHandler> _handlers;

    public WorkflowBusinessHandlerResolver(IEnumerable<IWorkflowBusinessHandler> handlers)
    {
        _handlers = handlers
            .GroupBy(handler => handler.BusinessType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IWorkflowBusinessHandler Resolve(string businessType)
    {
        if (string.IsNullOrWhiteSpace(businessType))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Business type is required.");
        }

        return _handlers.TryGetValue(businessType.Trim(), out var handler)
            ? handler
            : throw new BusinessException(ErrorCode.NotFound, $"Workflow business handler for '{businessType}' was not found.");
    }
}
