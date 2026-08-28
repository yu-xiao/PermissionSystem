using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiUserFeedback : BaseEntity
{
    public Guid RunId { get; set; }

    public Guid MessageId { get; set; }

    public Guid UserId { get; set; }

    public AiFeedbackRating Rating { get; set; }

    public string? ReasonCode { get; set; }

    public string? Comment { get; set; }
}
