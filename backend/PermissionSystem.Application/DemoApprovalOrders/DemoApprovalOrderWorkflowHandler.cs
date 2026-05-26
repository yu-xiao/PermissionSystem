using PermissionSystem.Application.Workflows;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.DemoApprovalOrders;

public sealed class DemoApprovalOrderWorkflowHandler : IWorkflowBusinessHandler
{
    private readonly IRepository<DemoApprovalOrder> _orderRepository;

    public DemoApprovalOrderWorkflowHandler(IRepository<DemoApprovalOrder> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public string BusinessType => DemoApprovalOrderConstants.BusinessType;

    public Task OnWorkflowStartedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        var order = GetOrderOrThrow(context);
        if (order.ApprovalStatus is not (ApprovalStatus.Draft or ApprovalStatus.Rejected or ApprovalStatus.Withdrawn))
        {
            throw new BusinessException(ErrorCode.Conflict, "Only draft, rejected or withdrawn demo approval orders can be submitted.");
        }

        order.ApprovalStatus = ApprovalStatus.Pending;
        order.WorkflowInstanceId = context.WorkflowInstanceId;
        order.SubmittedAt = DateTimeOffset.UtcNow;
        order.SubmittedBy = context.StarterUserId;
        order.ApprovedAt = null;
        order.RejectedAt = null;
        order.WithdrawnAt = null;
        _orderRepository.Update(order);
        return Task.CompletedTask;
    }

    public Task OnWorkflowApprovedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        var order = GetOrderOrThrow(context);
        order.ApprovalStatus = ApprovalStatus.Approved;
        order.ApprovedAt = DateTimeOffset.UtcNow;
        _orderRepository.Update(order);
        return Task.CompletedTask;
    }

    public Task OnWorkflowRejectedAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        var order = GetOrderOrThrow(context);
        order.ApprovalStatus = ApprovalStatus.Rejected;
        order.RejectedAt = DateTimeOffset.UtcNow;
        _orderRepository.Update(order);
        return Task.CompletedTask;
    }

    public Task OnWorkflowWithdrawnAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        var order = GetOrderOrThrow(context);
        order.ApprovalStatus = ApprovalStatus.Withdrawn;
        order.WithdrawnAt = DateTimeOffset.UtcNow;
        _orderRepository.Update(order);
        return Task.CompletedTask;
    }

    public Task OnWorkflowCancelledAsync(WorkflowBusinessContext context, CancellationToken cancellationToken)
    {
        var order = GetOrderOrThrow(context);
        order.ApprovalStatus = ApprovalStatus.Cancelled;
        _orderRepository.Update(order);
        return Task.CompletedTask;
    }

    private DemoApprovalOrder GetOrderOrThrow(WorkflowBusinessContext context)
    {
        return Guid.TryParse(context.BusinessId, out var orderId)
            ? _orderRepository.Query().FirstOrDefault(entity => entity.Id == orderId)
                ?? throw new BusinessException(ErrorCode.NotFound, "Demo approval order was not found.")
            : throw new BusinessException(ErrorCode.ValidationFailed, "Demo approval order business id is invalid.");
    }
}
