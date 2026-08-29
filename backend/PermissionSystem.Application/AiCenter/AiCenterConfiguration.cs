namespace PermissionSystem.Application.AiCenter;

public interface IAiCenterConfiguration
{
    bool Enabled { get; }

    IReadOnlyCollection<Guid> AllowedTenantIds { get; }

    int ConversationRetentionDays { get; }

    int AuditRetentionDays { get; }

    int RunWatchdogIntervalSeconds => 30;

    int RunOrphanTimeoutSeconds => 180;

    int RequestLimitPerMinute => 30;

    int ConcurrentRunLimit => 3;

    int TokenLimitPerHour => 100_000;
}

internal sealed class DefaultAiCenterConfiguration : IAiCenterConfiguration
{
    public bool Enabled => false;

    public IReadOnlyCollection<Guid> AllowedTenantIds => [];

    public int ConversationRetentionDays => 30;

    public int AuditRetentionDays => 180;

    public int RunWatchdogIntervalSeconds => 30;

    public int RunOrphanTimeoutSeconds => 180;

    public int RequestLimitPerMinute => 30;

    public int ConcurrentRunLimit => 3;

    public int TokenLimitPerHour => 100_000;
}
