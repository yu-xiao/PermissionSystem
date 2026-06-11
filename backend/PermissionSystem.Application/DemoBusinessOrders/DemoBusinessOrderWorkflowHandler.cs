using PermissionSystem.Application.StateMachines;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.DemoBusinessOrders;

public sealed class DemoBusinessOrderWorkflowHandler : IWorkflowBusinessHandler
{
    private readonly IRepository<DemoBusinessOrder> _orderRepository;
    private readonly IStateTransitionExecutor _stateTransitionExecutor;

    public DemoBusinessOrderWorkflowHandler(
        IRepository<DemoBusinessOrder> orderRepository,
        IStateTransitionExecutor stateTransitionExecutor)
    {
        _orderRepository = orderRepository;
        _stateTransitionExecutor = stateTransitionExecutor;
    }

    public string BusinessType => DemoBusinessOrderConstants.BusinessType;

    public async Task OnWorkflowStartedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        var order = GetOrderOrThrow(context.BusinessId);
        order.WorkflowInstanceId = context.WorkflowInstanceId;
        _orderRepository.Update(order);

        await _stateTransitionExecutor.ExecuteTransitionAsync(
            context.BusinessType,
            context.BusinessId,
            "Submit",
            context.Comment,
            cancellationToken);
    }

    public Task OnWorkflowApprovedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        return _stateTransitionExecutor.ExecuteTransitionAsync(
            context.BusinessType,
            context.BusinessId,
            "Approve",
            context.Comment,
            cancellationToken);
    }

    public Task OnWorkflowRejectedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        return _stateTransitionExecutor.ExecuteTransitionAsync(
            context.BusinessType,
            context.BusinessId,
            "Reject",
            context.Comment,
            cancellationToken);
    }

    public Task OnWorkflowWithdrawnAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        return _stateTransitionExecutor.ExecuteTransitionAsync(
            context.BusinessType,
            context.BusinessId,
            "Withdraw",
            context.Comment,
            cancellationToken);
    }

    public Task OnWorkflowCancelledAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        return _stateTransitionExecutor.ExecuteTransitionAsync(
            context.BusinessType,
            context.BusinessId,
            "Cancel",
            context.Comment,
            cancellationToken);
    }

    private DemoBusinessOrder GetOrderOrThrow(string businessId)
    {
        return Guid.TryParse(businessId, out var orderId)
            ? _orderRepository.Query().FirstOrDefault(entity => entity.Id == orderId)
                ?? throw new BusinessException(ErrorCode.NotFound, "Demo business order was not found.")
            : throw new BusinessException(ErrorCode.ValidationFailed, "Demo business order business id is invalid.");
    }
}
