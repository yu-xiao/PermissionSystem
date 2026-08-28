namespace PermissionSystem.Application.AiCenter;

public interface IAiCenterConfiguration
{
    bool Enabled { get; }

    IReadOnlyCollection<Guid> AllowedTenantIds { get; }

    int ConversationRetentionDays { get; }

    int AuditRetentionDays { get; }
}

internal sealed class DefaultAiCenterConfiguration : IAiCenterConfiguration
{
    public bool Enabled => false;

    public IReadOnlyCollection<Guid> AllowedTenantIds => [];

    public int ConversationRetentionDays => 30;

    public int AuditRetentionDays => 180;
}
