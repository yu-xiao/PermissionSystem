namespace PermissionSystem.Infrastructure.Options;

public sealed class LogArchiveOptions
{
    public const string SectionName = "LogArchive";

    public bool Enabled { get; init; } = true;

    public string ActiveLogDirectory { get; init; } = "logs";

    public string ArchiveDirectory { get; init; } = "logs/archive";

    public int ActiveRetentionDays { get; init; } = 7;

    public int ArchiveRetentionDays { get; init; } = 45;

    public int CleanupIntervalMinutes { get; init; } = 60;
}
