using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class ReportQueryParam : BaseEntity
{
    public Guid ReportId { get; set; }

    public string ParamCode { get; set; } = string.Empty;

    public string ParamName { get; set; } = string.Empty;

    public string ParamType { get; set; } = "String";

    public string? DefaultValue { get; set; }

    public bool Required { get; set; }

    public int Sort { get; set; }
}
