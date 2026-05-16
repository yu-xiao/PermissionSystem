namespace PermissionSystem.Infrastructure.Options;

public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";

    public string DashboardPath { get; init; } = "/hangfire";

    public string SchemaName { get; init; } = "Hangfire";

    public int WorkerCount { get; init; } = Environment.ProcessorCount * 5;

    public string[] Queues { get; init; } = ["default"];
}
