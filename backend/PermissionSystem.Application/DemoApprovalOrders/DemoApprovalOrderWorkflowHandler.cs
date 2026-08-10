using PermissionSystem.Application.Workflows;
using PermissionSystem.Application.StateMachines;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.DemoApprovalOrders;

[DataPermissionExempt("Workflow callbacks resolve a previously authorized business id inside the workflow transaction.")]
public sealed class DemoApprovalOrderWorkflowHandler : IWorkflowBusinessHandler
{
    private readonly IRepository<DemoApprovalOrder> _orderRepository;
    private readonly IStateTransitionExecutor _stateTransitionExecutor;

    public DemoApprovalOrderWorkflowHandler(
        IRepository<DemoApprovalOrder> orderRepository,
        IStateTransitionExecutor stateTransitionExecutor)
    {
        _orderRepository = orderRepository;
        _stateTransitionExecutor = stateTransitionExecutor;
    }

    public string BusinessType => DemoApprovalOrderConstants.BusinessType;

    public async Task OnWorkflowStartedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        var order = GetOrderOrThrow(context);
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

    private DemoApprovalOrder GetOrderOrThrow(WorkflowBusinessContext context)
    {
        return Guid.TryParse(context.BusinessId, out var orderId)
            ? _orderRepository.Query().FirstOrDefault(entity => entity.Id == orderId)
                ?? throw new BusinessException(ErrorCode.NotFound, "Demo approval order was not found.")
            : throw new BusinessException(ErrorCode.ValidationFailed, "Demo approval order business id is invalid.");
    }
}
