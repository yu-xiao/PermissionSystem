namespace PermissionSystem.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string InstanceName { get; init; } = "permission-system:";
}
