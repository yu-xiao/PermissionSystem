using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Reports;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AiContextReference(Guid RunId, string InvocationId);

public sealed class AiQueryContext
{
    public int Version { get; init; } = 1;
    public Dictionary<string, JsonElement> Parameters { get; init; } = new(StringComparer.Ordinal);
    public string? DataScopeFingerprint { get; init; }
}

public sealed class AiUserTableRow
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public Guid? DepartmentId { get; init; }
    public bool IsEnabled { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class AiUserTableData
{
    public long TotalCount { get; init; }
    public List<AiUserTableRow> Items { get; init; } = [];
    public int DisplayedRowCount => Items.Count;
}

public sealed record AiCountGroup(string Key, long Count);

public sealed class AiStatisticsGroup
{
    public string Code { get; init; } = string.Empty;
    public long TotalGroupCount { get; init; }
    public List<AiCountGroup> Items { get; init; } = [];
    public int DisplayedGroupCount => Items.Count;
    public bool IsTruncated => TotalGroupCount > Items.Count;
}

public sealed class AiStatisticsData
{
    public long TotalCount { get; init; }
    public List<AiStatisticsGroup> Groups { get; init; } = [];
}

public sealed class AiStructuredResult
{
    public Guid RunId { get; set; }
    public string InvocationId { get; set; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int Version { get; init; } = 1;
    public string ToolCode { get; init; } = string.Empty;
    public string ToolVersion { get; init; } = string.Empty;
    public DateTimeOffset QueriedAt { get; init; }
    public string EvaluationBasis { get; init; } = string.Empty;
    public AiQueryContext Context { get; init; } = new();
    public AiToolCitation Citation { get; set; } = new();
    public bool IsTruncated { get; set; }
    public IReadOnlyList<string> Limitations { get; init; } = [];
    public PermissionDiagnosticResponse? Diagnostic { get; init; }
    public AiUserTableData? Table { get; init; }
    public AiStatisticsData? Statistics { get; init; }
    public ReportUserMetrics? Metrics { get; init; }
    public AiReportResultMetadata? Report { get; init; }
}

public sealed class AiReportResultMetadata
{
    public Guid ReportDefinitionId { get; init; }
    public string DatasetKey { get; init; } = string.Empty;
    public string DatasetVersion { get; init; } = string.Empty;
    public string DefinitionFingerprint { get; init; } = string.Empty;

    public static string Fingerprint(ReportDefinition definition) => AiStructuredResults.Digest(JsonSerializer.Serialize(new
    {
        definition.Id, definition.TenantId, definition.DatasetKey, definition.DataSourceType, definition.UpdatedAt, definition.RowVersion
    }, AiStructuredResults.JsonOptions));
}

public sealed class AiStructuredResultEnvelope
{
    public string Type { get; init; } = "structured-result";
    public int Version { get; init; } = 1;
    public AiStructuredResult Result { get; init; } = new();
}

public sealed record AiStructuredResultPage(
    IReadOnlyList<AiStructuredResult> Results, bool HasUnavailableResults = false, bool IsWindowLimited = false);

public static class AiStructuredResults
{
    public const int MaxEnvelopeBytes = 64 * 1024;
    public const int MaxContextBytes = 4 * 1024;
    public const int MaxResponseBytes = 256 * 1024;
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static string Digest(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static Dictionary<string, JsonElement> Parameters(object parameters) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(parameters, JsonOptions), JsonOptions)!;

    public static string ScopeFingerprint(DataScopeContext scope) => Digest(JsonSerializer.Serialize(new
    {
        scope.ScopeType, scope.CurrentUserId, scope.CurrentDepartmentId, scope.IncludesCurrentUser,
        DepartmentIds = scope.DepartmentIds.Distinct().Order().ToArray()
    }, JsonOptions));

    public static bool IsSupported(string toolCode) => toolCode is
        PermissionDiagnosticAiToolHandler.ToolCode or "permission.users.search" or
        "permission.login_logs.summary" or "permission.operation_logs.summary" or "permission.reports.query_dataset";

    public static bool SupportsVersion(string toolCode, string version) => toolCode == "permission.reports.query_dataset"
        ? version == "2.0" : IsSupported(toolCode) && version == "1.0";

    public static string ResultType(string toolCode) => toolCode switch
    {
        PermissionDiagnosticAiToolHandler.ToolCode => "permission-diagnostic",
        "permission.users.search" => "table",
        "permission.login_logs.summary" or "permission.operation_logs.summary" => "statistics-summary",
        "permission.reports.query_dataset" => "controlled-report",
        _ => throw new BusinessException(ErrorCode.ValidationFailed, "Unsupported structured result tool.")
    };

    public static string[] AllowedParameters(string toolCode) => toolCode switch
    {
        PermissionDiagnosticAiToolHandler.ToolCode => ["kind", "targetUserId", "menuId", "permissionCode"],
        "permission.users.search" => ["keyword", "isEnabled", "limit", "departmentScope"],
        "permission.login_logs.summary" => ["userName", "startTime", "endTime"],
        "permission.operation_logs.summary" => ["userName", "module", "startTime", "endTime"],
        "permission.reports.query_dataset" => ["reportDefinitionId", "mode", "dimension", "sort", "limit", "params"],
        _ => []
    };

    public static string SerializeBounded(AiStructuredResult result)
    {
        if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(result.Context, JsonOptions)) > MaxContextBytes)
            throw new BusinessException(ErrorCode.ValidationFailed, "AI query context is too large.");
        while (true)
        {
            var displayedRowCount = result.Table?.DisplayedRowCount ?? result.Metrics?.DisplayedGroupCount;
            if (displayedRowCount.HasValue)
                result.Citation = new()
                {
                    SourceSystem = result.Citation.SourceSystem, ToolCode = result.Citation.ToolCode, ToolVersion = result.Citation.ToolVersion,
                    DatasetCode = result.Citation.DatasetCode, DatasetVersion = result.Citation.DatasetVersion,
                    QueryParametersDigest = result.Citation.QueryParametersDigest, QueriedAt = result.Citation.QueriedAt,
                    AsOf = result.Citation.AsOf, RowCount = displayedRowCount.Value
                };
            var content = JsonSerializer.Serialize(new AiStructuredResultEnvelope { Result = result }, JsonOptions);
            if (Encoding.UTF8.GetByteCount(content) <= MaxEnvelopeBytes) return content;
            if (result.Table?.Items.Count > 0) result.Table.Items.RemoveAt(result.Table.Items.Count - 1);
            else
            {
                if (result.Metrics?.Groups.Count > 0)
                {
                    result.Metrics.Groups.RemoveAt(result.Metrics.Groups.Count - 1);
                    result.IsTruncated = true;
                    continue;
                }
                var group = result.Statistics?.Groups.LastOrDefault(item => item.Items.Count > 0);
                if (group is null) throw new BusinessException(ErrorCode.ValidationFailed, "AI result metadata is too large.");
                group.Items.RemoveAt(group.Items.Count - 1);
            }
            result.IsTruncated = true;
        }
    }
}
