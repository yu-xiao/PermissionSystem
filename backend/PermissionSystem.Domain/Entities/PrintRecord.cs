using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class PrintRecord : BaseEntity
{
    public Guid TemplateId { get; set; }

    public string BusinessType { get; set; } = string.Empty;

    public string BusinessId { get; set; } = string.Empty;

    public Guid? PrintUserId { get; set; }

    public string? PrintUserName { get; set; }

    public DateTimeOffset PrintedAt { get; set; }

    public int PrintCount { get; set; } = 1;
}
