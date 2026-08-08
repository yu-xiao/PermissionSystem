using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class DemoBusinessOrder : BaseEntity, IApprovalBusinessEntity
{
    public string OrderNo { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Guid? DepartmentId { get; set; }

    public Guid OwnerUserId { get; set; }

    public string OwnerUserName { get; set; } = string.Empty;

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    public Guid? WorkflowInstanceId { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public Guid? SubmittedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? RejectedAt { get; set; }

    public DateTimeOffset? WithdrawnAt { get; set; }

    public string ChangeHistoryJson { get; set; } = "[]";

    public byte[] RowVersion { get; set; } = [];
}
