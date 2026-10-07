using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiAnomalies;

public static class AiAnomalyContract
{
    public const string JobType = "AiDemoPendingReminder";
    public const string SourceKind = "ai-demo-pending";
    public const string ViewPermission = "system:scheduled-task:view";
    public const string CreatePermission = "system:scheduled-task:create";
    public const string UpdatePermission = "system:scheduled-task:update";
    public const string TriggerPermission = "system:scheduled-task:trigger";
    public const string NotificationPermission = "system:notification:view";
    public const string Cron = "*/5 * * * *";
    public const string Basis = "Demo 单据当前授权范围内 Pending 完整数量 ≥ 1；本人按 CreatedBy、部门按 DepartmentId；非审批超时规则。";
    public static TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "China Standard Time" : "Asia/Shanghai");
    public static string Fingerprint(DataScopeContext scope) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { scope.ScopeType, scope.CurrentUserId, scope.CurrentDepartmentId,
            scope.IncludesCurrentUser, DepartmentIds = scope.DepartmentIds.Distinct().Order().ToArray() }))));
}

public sealed class AiAnomalyChangeRequest
{
    public byte[] RowVersion { get; init; } = [];
    public bool IsEnabled { get; init; }
}
public sealed class AiAnomalyCloseRequest { public byte[] RowVersion { get; init; } = []; }
public sealed record AiAnomalyRuleResponse(Guid Id, bool IsEnabled, DateTimeOffset? LastNotifiedAt,
    DateTimeOffset? LastRunAt, bool? LastRunSucceeded, string? LastRunMessage, byte[] RowVersion,
    string Basis = AiAnomalyContract.Basis, string TimeZone = "Asia/Shanghai", int IntervalMinutes = 5, int CooldownHours = 24);
public sealed record AiAnomalyEventResponse(Guid Id, Guid RuleId, long EpisodeSequence, long? ObservedCount,
    DateTimeOffset ObservedAt, DateTimeOffset? ClosedAt, string? CloseReason, string DeliveryStatus,
    int AttemptCount, DateTimeOffset? NextAttemptAt, string? ErrorCode, byte[] RowVersion, bool EvidenceUnavailable,
    string? TransportStatus, string Basis = AiAnomalyContract.Basis);

public interface IAiAnomalyService
{
    Task<PagedResult<AiAnomalyRuleResponse>> ListAsync(int pageIndex, int pageSize, CancellationToken ct = default);
    Task<AiAnomalyRuleResponse> CreateAsync(CancellationToken ct = default);
    Task<AiAnomalyRuleResponse> SetEnabledAsync(Guid id, AiAnomalyChangeRequest request, CancellationToken ct = default);
    Task TriggerAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AiAnomalyEventResponse>> EventsAsync(Guid ruleId, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<AiAnomalyEventResponse> EventAsync(Guid eventId, CancellationToken ct = default);
    Task CloseAsync(Guid eventId, byte[] rowVersion, CancellationToken ct = default);
}

public interface IAiAnomalyCommitFence { Task HoldAsync(Guid ruleId, CancellationToken ct); }
public interface IAiAnomalyExecutionHost
{
    Task ExecuteTaskAsync(Guid taskId, CancellationToken ct);
    Task RecordSkippedAsync(Guid taskId, CancellationToken ct);
    Task DeliverAsync(Guid tenantId, Guid eventId, CancellationToken ct);
}
