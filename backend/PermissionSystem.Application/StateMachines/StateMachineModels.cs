using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.StateMachines;

public sealed class StateMachineQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? BusinessType { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreateStateMachineRequest
{
    public string BusinessType { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public sealed class UpdateStateMachineRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public sealed class StateMachineResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class CreateOrUpdateStateRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string StateCode { get; init; } = string.Empty;

    public string StateName { get; init; } = string.Empty;

    public string StateType { get; init; } = "Normal";

    public string? Color { get; init; }

    public int Sort { get; init; }

    public bool IsInitial { get; init; }

    public bool IsFinal { get; init; }
}

public sealed class StateDefinitionResponse
{
    public Guid Id { get; init; }

    public Guid MachineId { get; init; }

    public string StateCode { get; init; } = string.Empty;

    public string StateName { get; init; } = string.Empty;

    public string StateType { get; init; } = string.Empty;

    public string? Color { get; init; }

    public int Sort { get; init; }

    public bool IsInitial { get; init; }

    public bool IsFinal { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class CreateOrUpdateTransitionRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string FromState { get; init; } = string.Empty;

    public string ToState { get; init; } = string.Empty;

    public string ActionCode { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;

    public string? RequiredPermission { get; init; }

    public string? ConditionJson { get; init; }

    public bool IsEnabled { get; init; } = true;

    public int Sort { get; init; }
}

public sealed class StateTransitionResponse
{
    public Guid Id { get; init; }

    public Guid MachineId { get; init; }

    public string FromState { get; init; } = string.Empty;

    public string ToState { get; init; } = string.Empty;

    public string ActionCode { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;

    public string? RequiredPermission { get; init; }

    public string? ConditionJson { get; init; }

    public bool IsEnabled { get; init; }

    public int Sort { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class StateTransitionLogQueryRequest : PaginationRequest
{
    public string? BusinessType { get; init; }

    public string? BusinessId { get; init; }

    public string? ActionCode { get; init; }
}

public sealed class StateTransitionLogResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public string FromState { get; init; } = string.Empty;

    public string ToState { get; init; } = string.Empty;

    public string ActionCode { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;

    public Guid? OperatorUserId { get; init; }

    public string? OperatorUserName { get; init; }

    public string? Comment { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class AvailableStateActionResponse
{
    public string ActionCode { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;

    public string FromState { get; init; } = string.Empty;

    public string ToState { get; init; } = string.Empty;

    public string? RequiredPermission { get; init; }
}

public sealed class ExecuteStateTransitionRequest
{
    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public string ActionCode { get; init; } = string.Empty;

    public string? Comment { get; init; }
}

public sealed class StateTransitionExecutionResponse
{
    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public string FromState { get; init; } = string.Empty;

    public string ToState { get; init; } = string.Empty;

    public string ActionCode { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;
}

public interface IStateMachineService
{
    Task<PagedResult<StateMachineResponse>> GetPagedAsync(
        StateMachineQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<StateMachineResponse> CreateAsync(CreateStateMachineRequest request, CancellationToken cancellationToken = default);

    Task<StateMachineResponse> UpdateAsync(Guid id, UpdateStateMachineRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StateDefinitionResponse>> GetStatesAsync(Guid machineId, CancellationToken cancellationToken = default);

    Task<StateDefinitionResponse> CreateStateAsync(Guid machineId, CreateOrUpdateStateRequest request, CancellationToken cancellationToken = default);

    Task<StateDefinitionResponse> UpdateStateAsync(Guid machineId, Guid stateId, CreateOrUpdateStateRequest request, CancellationToken cancellationToken = default);

    Task DeleteStateAsync(Guid machineId, Guid stateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StateTransitionResponse>> GetTransitionsAsync(Guid machineId, CancellationToken cancellationToken = default);

    Task<StateTransitionResponse> CreateTransitionAsync(Guid machineId, CreateOrUpdateTransitionRequest request, CancellationToken cancellationToken = default);

    Task<StateTransitionResponse> UpdateTransitionAsync(Guid machineId, Guid transitionId, CreateOrUpdateTransitionRequest request, CancellationToken cancellationToken = default);

    Task DeleteTransitionAsync(Guid machineId, Guid transitionId, CancellationToken cancellationToken = default);

    Task<PagedResult<StateTransitionLogResponse>> GetLogsAsync(StateTransitionLogQueryRequest request, CancellationToken cancellationToken = default);
}

public interface IStateTransitionExecutor
{
    Task<IReadOnlyList<AvailableStateActionResponse>> GetAvailableActionsAsync(
        string businessType,
        string businessId,
        string currentState,
        CancellationToken cancellationToken = default);

    Task<StateTransitionExecutionResponse> ExecuteTransitionAsync(
        string businessType,
        string businessId,
        string actionCode,
        string? comment = null,
        CancellationToken cancellationToken = default);

    Task ValidateTransitionAsync(
        string businessType,
        string businessId,
        string currentState,
        string actionCode,
        CancellationToken cancellationToken = default);
}

public sealed class StateTransitionContext
{
    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public string FromState { get; init; } = string.Empty;

    public string ToState { get; init; } = string.Empty;

    public string ActionCode { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public Guid? OperatorUserId { get; init; }

    public string? OperatorUserName { get; init; }
}

public interface IStateTransitionHandler : IScopedDependency
{
    string BusinessType { get; }

    Task<string> GetCurrentStateAsync(string businessId, CancellationToken cancellationToken = default);

    Task ValidateTransitionAsync(StateTransitionContext context, CancellationToken cancellationToken = default);

    Task OnTransitionAsync(StateTransitionContext context, CancellationToken cancellationToken = default);
}

public interface IStateTransitionHandlerResolver
{
    IStateTransitionHandler Resolve(string businessType);
}
