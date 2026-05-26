using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Common;

public interface IApprovalBusinessEntity
{
    ApprovalStatus ApprovalStatus { get; set; }

    Guid? WorkflowInstanceId { get; set; }

    DateTimeOffset? SubmittedAt { get; set; }

    Guid? SubmittedBy { get; set; }

    DateTimeOffset? ApprovedAt { get; set; }

    DateTimeOffset? RejectedAt { get; set; }

    DateTimeOffset? WithdrawnAt { get; set; }
}
