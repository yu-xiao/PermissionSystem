using PermissionSystem.Application.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.StateMachines;

public sealed class StateMachineService : IStateMachineService
{
    private readonly IRepository<StateMachineDefinition> _machineRepository;
    private readonly IRepository<StateDefinition> _stateRepository;
    private readonly IRepository<StateTransition> _transitionRepository;
    private readonly IRepository<StateTransitionLog> _logRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StateMachineService(
        IRepository<StateMachineDefinition> machineRepository,
        IRepository<StateDefinition> stateRepository,
        IRepository<StateTransition> transitionRepository,
        IRepository<StateTransitionLog> logRepository,
        IUnitOfWork unitOfWork)
    {
        _machineRepository = machineRepository;
        _stateRepository = stateRepository;
        _transitionRepository = transitionRepository;
        _logRepository = logRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<StateMachineResponse>> GetPagedAsync(
        StateMachineQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _machineRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.BusinessType.Contains(keyword) ||
                entity.Name.Contains(keyword) ||
                (entity.Description != null && entity.Description.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessType))
        {
            var businessType = request.BusinessType.Trim();
            query = query.Where(entity => entity.BusinessType == businessType);
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderBy(entity => entity.BusinessType)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<StateMachineResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<StateMachineResponse> CreateAsync(
        CreateStateMachineRequest request,
        CancellationToken cancellationToken = default)
    {
        var businessType = TrimRequired(request.BusinessType, "Business type is required.");
        if (_machineRepository.Query().Any(entity => entity.BusinessType == businessType))
        {
            throw new BusinessException(ErrorCode.Conflict, "State machine for this business type already exists.");
        }

        var machine = new StateMachineDefinition
        {
            BusinessType = businessType,
            Name = TrimRequired(request.Name, "State machine name is required."),
            Description = NormalizeOptional(request.Description),
            IsEnabled = request.IsEnabled
        };

        await _machineRepository.AddAsync(machine, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(machine);
    }

    public async Task<StateMachineResponse> UpdateAsync(
        Guid id,
        UpdateStateMachineRequest request,
        CancellationToken cancellationToken = default)
    {
        var machine = await GetMachineOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(machine, request.ConcurrencyToken);
        machine.Name = TrimRequired(request.Name, "State machine name is required.");
        machine.Description = NormalizeOptional(request.Description);
        machine.IsEnabled = request.IsEnabled;

        _machineRepository.Update(machine);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(machine);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var machine = await GetMachineOrThrowAsync(id, cancellationToken);
        foreach (var transition in _transitionRepository.Query().Where(entity => entity.MachineId == id).ToList())
        {
            _transitionRepository.Remove(transition);
        }

        foreach (var state in _stateRepository.Query().Where(entity => entity.MachineId == id).ToList())
        {
            _stateRepository.Remove(state);
        }

        _machineRepository.Remove(machine);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<StateDefinitionResponse>> GetStatesAsync(
        Guid machineId,
        CancellationToken cancellationToken = default)
    {
        EnsureMachineExists(machineId);
        var states = _stateRepository.Query()
            .Where(entity => entity.MachineId == machineId)
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.StateCode)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult<IReadOnlyList<StateDefinitionResponse>>(states);
    }

    public async Task<StateDefinitionResponse> CreateStateAsync(
        Guid machineId,
        CreateOrUpdateStateRequest request,
        CancellationToken cancellationToken = default)
    {
        var machine = await GetMachineOrThrowAsync(machineId, cancellationToken);
        var stateCode = TrimRequired(request.StateCode, "State code is required.");
        if (_stateRepository.Query().Any(entity => entity.MachineId == machineId && entity.StateCode == stateCode))
        {
            throw new BusinessException(ErrorCode.Conflict, "State code already exists.");
        }

        if (request.IsInitial)
        {
            ClearInitialState(machineId);
        }

        var state = new StateDefinition
        {
            TenantId = machine.TenantId,
            MachineId = machineId,
            StateCode = stateCode,
            StateName = TrimRequired(request.StateName, "State name is required."),
            StateType = TrimRequired(request.StateType, "State type is required."),
            Color = NormalizeOptional(request.Color),
            Sort = request.Sort,
            IsInitial = request.IsInitial,
            IsFinal = request.IsFinal
        };

        await _stateRepository.AddAsync(state, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(state);
    }

    public async Task<StateDefinitionResponse> UpdateStateAsync(
        Guid machineId,
        Guid stateId,
        CreateOrUpdateStateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureMachineExists(machineId);
        var state = await GetStateOrThrowAsync(machineId, stateId, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(state, request.ConcurrencyToken);
        var stateCode = TrimRequired(request.StateCode, "State code is required.");
        if (_stateRepository.Query().Any(entity => entity.MachineId == machineId && entity.Id != stateId && entity.StateCode == stateCode))
        {
            throw new BusinessException(ErrorCode.Conflict, "State code already exists.");
        }

        if (request.IsInitial)
        {
            ClearInitialState(machineId, stateId);
        }

        state.StateCode = stateCode;
        state.StateName = TrimRequired(request.StateName, "State name is required.");
        state.StateType = TrimRequired(request.StateType, "State type is required.");
        state.Color = NormalizeOptional(request.Color);
        state.Sort = request.Sort;
        state.IsInitial = request.IsInitial;
        state.IsFinal = request.IsFinal;

        _stateRepository.Update(state);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(state);
    }

    public async Task DeleteStateAsync(Guid machineId, Guid stateId, CancellationToken cancellationToken = default)
    {
        var state = await GetStateOrThrowAsync(machineId, stateId, cancellationToken);
        if (_transitionRepository.Query().Any(entity => entity.MachineId == machineId &&
            (entity.FromState == state.StateCode || entity.ToState == state.StateCode)))
        {
            throw new BusinessException(ErrorCode.Conflict, "State is used by transitions.");
        }

        _stateRepository.Remove(state);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<StateTransitionResponse>> GetTransitionsAsync(
        Guid machineId,
        CancellationToken cancellationToken = default)
    {
        EnsureMachineExists(machineId);
        var transitions = _transitionRepository.Query()
            .Where(entity => entity.MachineId == machineId)
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.ActionCode)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult<IReadOnlyList<StateTransitionResponse>>(transitions);
    }

    public async Task<StateTransitionResponse> CreateTransitionAsync(
        Guid machineId,
        CreateOrUpdateTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var machine = await GetMachineOrThrowAsync(machineId, cancellationToken);
        ValidateTransitionStates(machineId, request.FromState, request.ToState);

        var transition = new StateTransition
        {
            TenantId = machine.TenantId,
            MachineId = machineId,
            FromState = TrimRequired(request.FromState, "From state is required."),
            ToState = TrimRequired(request.ToState, "To state is required."),
            ActionCode = TrimRequired(request.ActionCode, "Action code is required."),
            ActionName = TrimRequired(request.ActionName, "Action name is required."),
            RequiredPermission = NormalizeOptional(request.RequiredPermission),
            ConditionJson = NormalizeOptional(request.ConditionJson),
            IsEnabled = request.IsEnabled,
            Sort = request.Sort
        };

        await _transitionRepository.AddAsync(transition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(transition);
    }

    public async Task<StateTransitionResponse> UpdateTransitionAsync(
        Guid machineId,
        Guid transitionId,
        CreateOrUpdateTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureMachineExists(machineId);
        ValidateTransitionStates(machineId, request.FromState, request.ToState);

        var transition = await GetTransitionOrThrowAsync(machineId, transitionId, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(transition, request.ConcurrencyToken);
        transition.FromState = TrimRequired(request.FromState, "From state is required.");
        transition.ToState = TrimRequired(request.ToState, "To state is required.");
        transition.ActionCode = TrimRequired(request.ActionCode, "Action code is required.");
        transition.ActionName = TrimRequired(request.ActionName, "Action name is required.");
        transition.RequiredPermission = NormalizeOptional(request.RequiredPermission);
        transition.ConditionJson = NormalizeOptional(request.ConditionJson);
        transition.IsEnabled = request.IsEnabled;
        transition.Sort = request.Sort;

        _transitionRepository.Update(transition);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(transition);
    }

    public async Task DeleteTransitionAsync(Guid machineId, Guid transitionId, CancellationToken cancellationToken = default)
    {
        var transition = await GetTransitionOrThrowAsync(machineId, transitionId, cancellationToken);
        _transitionRepository.Remove(transition);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<PagedResult<StateTransitionLogResponse>> GetLogsAsync(
        StateTransitionLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _logRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.BusinessType))
        {
            var businessType = request.BusinessType.Trim();
            query = query.Where(entity => entity.BusinessType == businessType);
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessId))
        {
            var businessId = request.BusinessId.Trim();
            query = query.Where(entity => entity.BusinessId == businessId);
        }

        if (!string.IsNullOrWhiteSpace(request.ActionCode))
        {
            var actionCode = request.ActionCode.Trim();
            query = query.Where(entity => entity.ActionCode == actionCode);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<StateTransitionLogResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    private async Task<StateMachineDefinition> GetMachineOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _machineRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "State machine was not found.");
    }

    private void EnsureMachineExists(Guid machineId)
    {
        if (!_machineRepository.Query().Any(entity => entity.Id == machineId))
        {
            throw new BusinessException(ErrorCode.NotFound, "State machine was not found.");
        }
    }

    private async Task<StateDefinition> GetStateOrThrowAsync(Guid machineId, Guid stateId, CancellationToken cancellationToken)
    {
        var state = await _stateRepository.GetByIdAsync(stateId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "State was not found.");

        if (state.MachineId != machineId)
        {
            throw new BusinessException(ErrorCode.NotFound, "State was not found.");
        }

        return state;
    }

    private async Task<StateTransition> GetTransitionOrThrowAsync(Guid machineId, Guid transitionId, CancellationToken cancellationToken)
    {
        var transition = await _transitionRepository.GetByIdAsync(transitionId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Transition was not found.");

        if (transition.MachineId != machineId)
        {
            throw new BusinessException(ErrorCode.NotFound, "Transition was not found.");
        }

        return transition;
    }

    private void ClearInitialState(Guid machineId, Guid? exceptStateId = null)
    {
        foreach (var state in _stateRepository.Query()
            .Where(entity => entity.MachineId == machineId && entity.IsInitial && (!exceptStateId.HasValue || entity.Id != exceptStateId.Value))
            .ToList())
        {
            state.IsInitial = false;
            _stateRepository.Update(state);
        }
    }

    private void ValidateTransitionStates(Guid machineId, string fromState, string toState)
    {
        var normalizedFromState = TrimRequired(fromState, "From state is required.");
        var normalizedToState = TrimRequired(toState, "To state is required.");
        var states = _stateRepository.Query()
            .Where(entity => entity.MachineId == machineId)
            .Select(entity => entity.StateCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!states.Contains(normalizedFromState) || !states.Contains(normalizedToState))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Transition states must exist in the state machine.");
        }
    }

    private static StateMachineResponse ToResponse(StateMachineDefinition machine)
    {
        return new StateMachineResponse
        {
            Id = machine.Id,
            TenantId = machine.TenantId,
            BusinessType = machine.BusinessType,
            Name = machine.Name,
            Description = machine.Description,
            IsEnabled = machine.IsEnabled,
            CreatedAt = machine.CreatedAt,
            ConcurrencyToken = machine.RowVersion
        };
    }

    private static StateDefinitionResponse ToResponse(StateDefinition state)
    {
        return new StateDefinitionResponse
        {
            Id = state.Id,
            MachineId = state.MachineId,
            StateCode = state.StateCode,
            StateName = state.StateName,
            StateType = state.StateType,
            Color = state.Color,
            Sort = state.Sort,
            IsInitial = state.IsInitial,
            IsFinal = state.IsFinal,
            ConcurrencyToken = state.RowVersion
        };
    }

    private static StateTransitionResponse ToResponse(StateTransition transition)
    {
        return new StateTransitionResponse
        {
            Id = transition.Id,
            MachineId = transition.MachineId,
            FromState = transition.FromState,
            ToState = transition.ToState,
            ActionCode = transition.ActionCode,
            ActionName = transition.ActionName,
            RequiredPermission = transition.RequiredPermission,
            ConditionJson = transition.ConditionJson,
            IsEnabled = transition.IsEnabled,
            Sort = transition.Sort,
            ConcurrencyToken = transition.RowVersion
        };
    }

    private static StateTransitionLogResponse ToResponse(StateTransitionLog log)
    {
        return new StateTransitionLogResponse
        {
            Id = log.Id,
            TenantId = log.TenantId,
            BusinessType = log.BusinessType,
            BusinessId = log.BusinessId,
            FromState = log.FromState,
            ToState = log.ToState,
            ActionCode = log.ActionCode,
            ActionName = log.ActionName,
            OperatorUserId = log.OperatorUserId,
            OperatorUserName = log.OperatorUserName,
            Comment = log.Comment,
            CreatedAt = log.CreatedAt
        };
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
