namespace PermissionSystem.Infrastructure.Options;

public sealed class ReportOptions
{
    public const string SectionName = "Reports";

    public bool SqlReportsEnabled { get; init; } = true;

    public int QueryTimeoutSeconds { get; init; } = 30;

    public int MaxRows { get; init; } = 1000;
}
