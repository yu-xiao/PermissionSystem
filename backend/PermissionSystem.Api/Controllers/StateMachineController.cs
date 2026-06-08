using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.StateMachines;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/system/state-machines")]
public sealed class StateMachineController : ApiControllerBase
{
    private readonly IStateMachineService _stateMachineService;
    private readonly IStateTransitionExecutor _stateTransitionExecutor;

    public StateMachineController(
        IStateMachineService stateMachineService,
        IStateTransitionExecutor stateTransitionExecutor)
    {
        _stateMachineService = stateMachineService;
        _stateTransitionExecutor = stateTransitionExecutor;
    }

    [HttpGet]
    [Permission("system:state-machine:view")]
    public async Task<ActionResult<ApiResult<PagedResult<StateMachineResponse>>>> GetPagedAsync(
        [FromQuery] StateMachineQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _stateMachineService.GetPagedAsync(request, cancellationToken));
    }

    [HttpPost]
    [Permission("system:state-machine:create")]
    public async Task<ActionResult<ApiResult<StateMachineResponse>>> CreateAsync(
        [FromBody] CreateStateMachineRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _stateMachineService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Permission("system:state-machine:update")]
    public async Task<ActionResult<ApiResult<StateMachineResponse>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateStateMachineRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _stateMachineService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:state-machine:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _stateMachineService.DeleteAsync(id, cancellationToken);
        return Success();
    }

    [HttpGet("{id:guid}/states")]
    [Permission("system:state-machine:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<StateDefinitionResponse>>>> GetStatesAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _stateMachineService.GetStatesAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/states")]
    [Permission("system:state-machine:update")]
    public async Task<ActionResult<ApiResult<StateDefinitionResponse>>> CreateStateAsync(
        Guid id,
        [FromBody] CreateOrUpdateStateRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _stateMachineService.CreateStateAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:guid}/states/{stateId:guid}")]
    [Permission("system:state-machine:update")]
    public async Task<ActionResult<ApiResult<StateDefinitionResponse>>> UpdateStateAsync(
        Guid id,
        Guid stateId,
        [FromBody] CreateOrUpdateStateRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _stateMachineService.UpdateStateAsync(id, stateId, request, cancellationToken));
    }

    [HttpDelete("{id:guid}/states/{stateId:guid}")]
    [Permission("system:state-machine:update")]
    public async Task<ActionResult<ApiResult>> DeleteStateAsync(
        Guid id,
        Guid stateId,
        CancellationToken cancellationToken)
    {
        await _stateMachineService.DeleteStateAsync(id, stateId, cancellationToken);
        return Success();
    }

    [HttpGet("{id:guid}/transitions")]
    [Permission("system:state-machine:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<StateTransitionResponse>>>> GetTransitionsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Success(await _stateMachineService.GetTransitionsAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/transitions")]
    [Permission("system:state-machine:update")]
    public async Task<ActionResult<ApiResult<StateTransitionResponse>>> CreateTransitionAsync(
        Guid id,
        [FromBody] CreateOrUpdateTransitionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _stateMachineService.CreateTransitionAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:guid}/transitions/{transitionId:guid}")]
    [Permission("system:state-machine:update")]
    public async Task<ActionResult<ApiResult<StateTransitionResponse>>> UpdateTransitionAsync(
        Guid id,
        Guid transitionId,
        [FromBody] CreateOrUpdateTransitionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _stateMachineService.UpdateTransitionAsync(id, transitionId, request, cancellationToken));
    }

    [HttpDelete("{id:guid}/transitions/{transitionId:guid}")]
    [Permission("system:state-machine:update")]
    public async Task<ActionResult<ApiResult>> DeleteTransitionAsync(
        Guid id,
        Guid transitionId,
        CancellationToken cancellationToken)
    {
        await _stateMachineService.DeleteTransitionAsync(id, transitionId, cancellationToken);
        return Success();
    }

    [HttpPost("transition")]
    [Permission("system:state-machine:transition")]
    public async Task<ActionResult<ApiResult<StateTransitionExecutionResponse>>> ExecuteTransitionAsync(
        [FromBody] ExecuteStateTransitionRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _stateTransitionExecutor.ExecuteTransitionAsync(
            request.BusinessType,
            request.BusinessId,
            request.ActionCode,
            request.Comment,
            cancellationToken));
    }

    [HttpGet("logs")]
    [Permission("system:state-machine:log")]
    public async Task<ActionResult<ApiResult<PagedResult<StateTransitionLogResponse>>>> GetLogsAsync(
        [FromQuery] StateTransitionLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _stateMachineService.GetLogsAsync(request, cancellationToken));
    }
}
