using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.StateMachines;

public sealed class StateTransitionExecutor : IStateTransitionExecutor
{
    private readonly IRepository<StateMachineDefinition> _machineRepository;
    private readonly IRepository<StateTransition> _transitionRepository;
    private readonly IRepository<StateTransitionLog> _logRepository;
    private readonly IStateTransitionHandlerResolver _handlerResolver;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public StateTransitionExecutor(
        IRepository<StateMachineDefinition> machineRepository,
        IRepository<StateTransition> transitionRepository,
        IRepository<StateTransitionLog> logRepository,
        IStateTransitionHandlerResolver handlerResolver,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _machineRepository = machineRepository;
        _transitionRepository = transitionRepository;
        _logRepository = logRepository;
        _handlerResolver = handlerResolver;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<AvailableStateActionResponse>> GetAvailableActionsAsync(
        string businessType,
        string businessId,
        string currentState,
        CancellationToken cancellationToken = default)
    {
        var machine = GetEnabledMachineOrThrow(businessType);
        var normalizedState = TrimRequired(currentState, "Current state is required.");

        var actions = _transitionRepository.Query()
            .Where(entity => entity.MachineId == machine.Id &&
                entity.FromState == normalizedState &&
                entity.IsEnabled)
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.ActionCode)
            .ToList()
            .Where(HasTransitionPermission)
            .Select(entity => new AvailableStateActionResponse
            {
                ActionCode = entity.ActionCode,
                ActionName = entity.ActionName,
                FromState = entity.FromState,
                ToState = entity.ToState,
                RequiredPermission = entity.RequiredPermission
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<AvailableStateActionResponse>>(actions);
    }

    public async Task<StateTransitionExecutionResponse> ExecuteTransitionAsync(
        string businessType,
        string businessId,
        string actionCode,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedBusinessType = TrimRequired(businessType, "Business type is required.");
        var normalizedBusinessId = TrimRequired(businessId, "Business id is required.");
        var normalizedActionCode = TrimRequired(actionCode, "Action code is required.");
        var handler = _handlerResolver.Resolve(normalizedBusinessType);
        StateTransitionExecutionResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var currentState = await handler.GetCurrentStateAsync(normalizedBusinessId, token);
            var transition = ResolveTransition(normalizedBusinessType, currentState, normalizedActionCode);
            EnsurePermission(transition);

            var context = new StateTransitionContext
            {
                BusinessType = normalizedBusinessType,
                BusinessId = normalizedBusinessId,
                FromState = transition.FromState,
                ToState = transition.ToState,
                ActionCode = transition.ActionCode,
                ActionName = transition.ActionName,
                Comment = NormalizeOptional(comment),
                OperatorUserId = _currentUserService.UserId,
                OperatorUserName = _currentUserService.Username
            };

            await handler.ValidateTransitionAsync(context, token);
            await handler.OnTransitionAsync(context, token);
            await _logRepository.AddAsync(new StateTransitionLog
            {
                BusinessType = context.BusinessType,
                BusinessId = context.BusinessId,
                FromState = context.FromState,
                ToState = context.ToState,
                ActionCode = context.ActionCode,
                ActionName = context.ActionName,
                OperatorUserId = context.OperatorUserId,
                OperatorUserName = context.OperatorUserName,
                Comment = context.Comment
            }, token);

            await _unitOfWork.SaveChangesAsync(token);
            response = new StateTransitionExecutionResponse
            {
                BusinessType = context.BusinessType,
                BusinessId = context.BusinessId,
                FromState = context.FromState,
                ToState = context.ToState,
                ActionCode = context.ActionCode,
                ActionName = context.ActionName
            };
        }, cancellationToken);

        return response!;
    }

    public Task ValidateTransitionAsync(
        string businessType,
        string businessId,
        string currentState,
        string actionCode,
        CancellationToken cancellationToken = default)
    {
        var transition = ResolveTransition(
            TrimRequired(businessType, "Business type is required."),
            TrimRequired(currentState, "Current state is required."),
            TrimRequired(actionCode, "Action code is required."));

        EnsurePermission(transition);
        return Task.CompletedTask;
    }

    private StateTransition ResolveTransition(string businessType, string currentState, string actionCode)
    {
        var machine = GetEnabledMachineOrThrow(businessType);
        return _transitionRepository.Query()
            .FirstOrDefault(entity =>
                entity.MachineId == machine.Id &&
                entity.FromState == currentState &&
                entity.ActionCode == actionCode &&
                entity.IsEnabled)
            ?? throw new BusinessException(ErrorCode.Conflict, "State transition is not allowed.");
    }

    private StateMachineDefinition GetEnabledMachineOrThrow(string businessType)
    {
        var normalizedBusinessType = TrimRequired(businessType, "Business type is required.");
        return _machineRepository.Query()
            .FirstOrDefault(entity => entity.BusinessType == normalizedBusinessType && entity.IsEnabled)
            ?? throw new BusinessException(ErrorCode.NotFound, "Enabled state machine was not found.");
    }

    private bool HasTransitionPermission(StateTransition transition)
    {
        return string.IsNullOrWhiteSpace(transition.RequiredPermission) ||
            _currentUserService.IsSuperAdmin ||
            _currentUserService.HasPermission(transition.RequiredPermission);
    }

    private void EnsurePermission(StateTransition transition)
    {
        if (!HasTransitionPermission(transition))
        {
            throw new BusinessException(ErrorCode.Forbidden, "You do not have permission to execute this state transition.");
        }
    }

    private static string TrimRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed class StateTransitionHandlerResolver : IStateTransitionHandlerResolver
{
    private readonly IReadOnlyDictionary<string, IStateTransitionHandler> _handlers;

    public StateTransitionHandlerResolver(IEnumerable<IStateTransitionHandler> handlers)
    {
        _handlers = handlers
            .GroupBy(handler => handler.BusinessType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IStateTransitionHandler Resolve(string businessType)
    {
        if (string.IsNullOrWhiteSpace(businessType))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Business type is required.");
        }

        return _handlers.TryGetValue(businessType.Trim(), out var handler)
            ? handler
            : throw new BusinessException(ErrorCode.NotFound, $"State transition handler for '{businessType}' was not found.");
    }
}
