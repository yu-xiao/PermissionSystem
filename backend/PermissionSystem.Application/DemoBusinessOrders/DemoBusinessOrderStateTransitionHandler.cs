using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.StateMachines;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.DemoBusinessOrders;

public sealed class DemoBusinessOrderStateTransitionHandler : IStateTransitionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<DemoBusinessOrder> _orderRepository;
    private readonly ICurrentUserService _currentUserService;

    public DemoBusinessOrderStateTransitionHandler(
        IRepository<DemoBusinessOrder> orderRepository,
        ICurrentUserService currentUserService)
    {
        _orderRepository = orderRepository;
        _currentUserService = currentUserService;
    }

    public string BusinessType => DemoBusinessOrderConstants.BusinessType;

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
        order.UpdatedBy = context.OperatorUserId;
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

        AppendChange(order, context);
        _orderRepository.Update(order);
        return Task.CompletedTask;
    }

    private DemoBusinessOrder GetOrderOrThrow(string businessId)
    {
        return Guid.TryParse(businessId, out var orderId)
            ? _orderRepository.Query().FirstOrDefault(entity => entity.Id == orderId)
                ?? throw new BusinessException(ErrorCode.NotFound, "Demo business order was not found.")
            : throw new BusinessException(ErrorCode.ValidationFailed, "Demo business order business id is invalid.");
    }

    private void AppendChange(DemoBusinessOrder order, StateTransitionContext context)
    {
        var changes = DeserializeChanges(order.ChangeHistoryJson).ToList();
        changes.Add(new DemoBusinessOrderChangeHistoryResponse
        {
            ChangedAt = DateTimeOffset.UtcNow,
            ChangedBy = context.OperatorUserId ?? _currentUserService.UserId,
            ChangedByName = context.OperatorUserName ?? _currentUserService.Username,
            Action = context.ActionCode,
            Description = $"{context.FromState} -> {context.ToState}. {context.Comment}".Trim()
        });
        order.ChangeHistoryJson = JsonSerializer.Serialize(changes, JsonOptions);
    }

    private static IReadOnlyList<DemoBusinessOrderChangeHistoryResponse> DeserializeChanges(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<DemoBusinessOrderChangeHistoryResponse>>(value, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
