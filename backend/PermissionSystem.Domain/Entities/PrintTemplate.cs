using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class PrintTemplate : BaseEntity
{
    public string TemplateCode { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;

    public string BusinessType { get; set; } = string.Empty;

    public string TemplateType { get; set; } = "Document";

    public string ContentHtml { get; set; } = string.Empty;

    public string? ContentJson { get; set; }

    public string PaperSize { get; set; } = "A4";

    public string Orientation { get; set; } = "Portrait";

    public bool IsDefault { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int Version { get; set; } = 1;

    public string? Remark { get; set; }
}
