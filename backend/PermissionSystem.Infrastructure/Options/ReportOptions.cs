namespace PermissionSystem.Infrastructure.Options;

public sealed class ReportOptions
{
    public const string SectionName = "Reports";

    public bool SqlReportsEnabled { get; init; }

    public string? ReportConnection { get; init; }

    public int QueryTimeoutSeconds { get; init; } = 30;

    public int MaxRows { get; init; } = 1000;

    public int MaxConcurrentQueries { get; init; } = 4;

    public IReadOnlyList<ReportDatasetOptions> Datasets { get; init; } = [];
}

public sealed class ReportDatasetOptions
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string ViewName { get; init; } = string.Empty;

    public IReadOnlyList<ReportDatasetFilterOptions> Filters { get; init; } = [];
}

public sealed class ReportDatasetFilterOptions
{
    public string ParamCode { get; init; } = string.Empty;

    public string ColumnName { get; init; } = string.Empty;

    public string Operator { get; init; } = "Equal";
}
