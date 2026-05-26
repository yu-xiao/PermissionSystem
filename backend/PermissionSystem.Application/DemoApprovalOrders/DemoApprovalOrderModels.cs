using PermissionSystem.Domain.Enums;
using PermissionSystem.Application.Workflows;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.DemoApprovalOrders;

public static class DemoApprovalOrderConstants
{
    public const string BusinessType = "DemoApprovalOrder";
}

public sealed class DemoApprovalOrderQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public ApprovalStatus? ApprovalStatus { get; init; }
}

public sealed class CreateDemoApprovalOrderRequest
{
    public Guid? TenantId { get; init; }

    public string OrderNo { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? DepartmentId { get; init; }
}

public sealed class UpdateDemoApprovalOrderRequest
{
    public string Title { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? DepartmentId { get; init; }
}

public sealed class SubmitDemoApprovalOrderRequest
{
    public string? Remark { get; init; }
}

public sealed class DemoApprovalOrderResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string OrderNo { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? DepartmentId { get; init; }

    public Guid ApplicantUserId { get; init; }

    public string ApplicantUserName { get; init; } = string.Empty;

    public ApprovalStatus ApprovalStatus { get; init; }

    public Guid? WorkflowInstanceId { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }

    public Guid? SubmittedBy { get; init; }

    public DateTimeOffset? ApprovedAt { get; init; }

    public DateTimeOffset? RejectedAt { get; init; }

    public DateTimeOffset? WithdrawnAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}

public interface IDemoApprovalOrderService
{
    Task<PagedResult<DemoApprovalOrderResponse>> GetPagedAsync(
        DemoApprovalOrderQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<DemoApprovalOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DemoApprovalOrderResponse> CreateAsync(
        CreateDemoApprovalOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<DemoApprovalOrderResponse> UpdateAsync(
        Guid id,
        UpdateDemoApprovalOrderRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DemoApprovalOrderResponse> SubmitAsync(
        Guid id,
        SubmitDemoApprovalOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<DemoApprovalOrderResponse> WithdrawAsync(
        Guid id,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken = default);
}
