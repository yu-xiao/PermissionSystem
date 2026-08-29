using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiTools;

namespace PermissionSystem.Infrastructure.Options;

public sealed class AiCenterOptions : IAiCenterConfiguration, IAiToolConfiguration, IAiDraftConfiguration
{
    public const string SectionName = "Ai";

    public bool Enabled { get; init; }

    public Guid[] AllowedTenantIds { get; init; } = [];

    public int ConversationRetentionDays { get; init; } = 30;

    public int AuditRetentionDays { get; init; } = 180;

    public int RunWatchdogIntervalSeconds { get; init; } = 30;

    public int RunOrphanTimeoutSeconds { get; init; } = 180;

    public int RequestLimitPerMinute { get; init; } = 30;

    public int ConcurrentRunLimit { get; init; } = 3;

    public int TokenLimitPerHour { get; init; } = 100_000;

    public bool EnableReportDatasetTool { get; init; }

    public string[] ApprovedReportDatasetKeys { get; init; } = [];

    public int MaxToolRows { get; init; } = 200;

    public int DraftExpirationMinutes { get; init; } = 30;

    public int ConfirmationExpirationMinutes { get; init; } = 2;

    public int DraftRetentionDays { get; init; } = 30;

    IReadOnlyCollection<Guid> IAiCenterConfiguration.AllowedTenantIds => AllowedTenantIds;

    IReadOnlyCollection<string> IAiToolConfiguration.ApprovedReportDatasetKeys => ApprovedReportDatasetKeys;
}
