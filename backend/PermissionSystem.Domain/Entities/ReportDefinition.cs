using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class ReportDefinition : BaseEntity
{
    public string ReportCode { get; set; } = string.Empty;

    public string ReportName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string DataSourceType { get; set; } = "Sql";

    public string? SqlText { get; set; }

    public string? ApiUrl { get; set; }

    public string? ColumnsJson { get; set; }

    public string? ParamsJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? Remark { get; set; }
}
