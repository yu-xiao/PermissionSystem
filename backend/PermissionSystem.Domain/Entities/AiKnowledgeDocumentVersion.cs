using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiKnowledgeDocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public Guid? FileResourceId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = "utf8-paragraph-1";
    public AiKnowledgeParseStatus ParseStatus { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
