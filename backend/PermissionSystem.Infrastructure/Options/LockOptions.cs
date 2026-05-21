namespace PermissionSystem.Infrastructure.Options;

public sealed class LockOptions
{
    public const string SectionName = "Lock";

    public int DefaultExpirySeconds { get; init; } = 30;

    public int DefaultWaitSeconds { get; init; } = 10;

    public int RetryDelayMilliseconds { get; init; } = 100;

    public string KeyPrefix { get; init; } = "ps:lock:";
}
