using PermissionSystem.Application.StateMachines;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.DemoApprovalOrders;

public sealed class DemoApprovalOrderStateTransitionHandler : IStateTransitionHandler
{
    private readonly IRepository<DemoApprovalOrder> _orderRepository;

    public DemoApprovalOrderStateTransitionHandler(IRepository<DemoApprovalOrder> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public string BusinessType => DemoApprovalOrderConstants.BusinessType;

    public Task<string> GetCurrentStateAsync(string businessId, CancellationToken cancellationToken = default)
    {
        var order = GetOrderOrThrow(businessId);
        return Task.FromResult(order.ApprovalStatus.ToString());
    }

    public Task ValidateTransitionAsync(StateTransitionContext context, CancellationToken cancellationToken = default)
    {
        _ = GetOrderOrThrow(context.BusinessId);
        return Task.CompletedTask;
    }

    public Task OnTransitionAsync(StateTransitionContext context, CancellationToken cancellationToken = default)
    {
        var order = GetOrderOrThrow(context.BusinessId);
        if (!Enum.TryParse<ApprovalStatus>(context.ToState, ignoreCase: true, out var targetStatus))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Target approval status is invalid.");
        }

        order.ApprovalStatus = targetStatus;
        var now = DateTimeOffset.UtcNow;
        switch (targetStatus)
        {
            case ApprovalStatus.Pending:
                order.SubmittedAt = now;
                order.SubmittedBy = context.OperatorUserId;
                order.ApprovedAt = null;
                order.RejectedAt = null;
                order.WithdrawnAt = null;
                break;
            case ApprovalStatus.Approved:
                order.ApprovedAt = now;
                break;
            case ApprovalStatus.Rejected:
                order.RejectedAt = now;
                break;
            case ApprovalStatus.Withdrawn:
                order.WithdrawnAt = now;
                break;
        }

        _orderRepository.Update(order);
        return Task.CompletedTask;
    }

    private DemoApprovalOrder GetOrderOrThrow(string businessId)
    {
        return Guid.TryParse(businessId, out var orderId)
            ? _orderRepository.Query().FirstOrDefault(entity => entity.Id == orderId)
                ?? throw new BusinessException(ErrorCode.NotFound, "Demo approval order was not found.")
            : throw new BusinessException(ErrorCode.ValidationFailed, "Demo approval order business id is invalid.");
    }
}
