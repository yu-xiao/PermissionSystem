using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Reports;

public static class ReportDatasetCapabilities
{
    public const string AllOnly = "AllOnly";
    public const string UserDirectory = "UserDirectoryV1";
    public const string UserDatasetKey = "system-users-scoped";
    public const string UserViewName = "reporting.AiSystemUsers";
    public const string UserVersion = "1.0";
    public const int MaxDepartmentIds = 10000;
    public const string EvaluationBasis = "当前租户非删除用户，包含内置用户；当前授权范围与筛选取交集；" +
        "一名用户计一人，启停按当前 IsEnabled，部门按当前 DepartmentId；创建时间起点含、终点不含。";
    public const string DataTimeLimitation = "实时读取当前状态及部门归属，非历史人员快照；无独立数据水位，普通读隔离不保证历史时点一致性。";
    public static string[] UserColumns => ["Id", "DepartmentId", "UserName", "DisplayName", "IsEnabled", "CreatedAt"];
    public static string[] UserFilterCodes => ["keyword", "isEnabled", "departmentId", "departmentScope", "startTime", "endTime"];
    public static IReadOnlyList<ReportMetricDefinition> MetricDefinitions =>
    [
        new("user-count", "用户总数", "匹配用户 ID 的人数，一人一条记录。", "人"),
        new("enabled-user-count", "启用人数", "同一匹配集内当前 IsEnabled=true 的人数。", "人"),
        new("disabled-user-count", "停用人数", "同一匹配集内当前 IsEnabled=false 的人数。", "人")
    ];

    public static string ValidateQuery(string mode, string dimension, string? sort, int? limit)
    {
        if (mode is not ("Rows" or "Metrics") || dimension is not ("None" or "IsEnabled" or "DepartmentId") ||
            (mode == "Rows" && dimension != "None") || limit is < 1 or > 10000)
            throw Invalid("Invalid controlled report mode, dimension or limit.");
        var expected = mode == "Rows" ? "UserName" : dimension == "DepartmentId" ? "CountDescending" : "Key";
        if (sort is not null && sort != expected) throw Invalid("Unsupported controlled report sort.");
        return expected;
    }

    public static ReportUserFilters NormalizeFilters(IReadOnlyDictionary<string, JsonElement> input,
        IReadOnlyList<ReportQueryParamResponse> definitions)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in input)
        {
            if (!UserFilterCodes.Contains(item.Key, StringComparer.OrdinalIgnoreCase) || !values.TryAdd(item.Key, item.Value))
                throw Invalid("Unknown or duplicate controlled report filter.");
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            if (!UserFilterCodes.Contains(definition.ParamCode, StringComparer.OrdinalIgnoreCase) || !seen.Add(definition.ParamCode))
                throw Invalid("Invalid controlled report parameter definition.");
            ValidateParameterType(definition.ParamCode, definition.ParamType);
            if (!values.ContainsKey(definition.ParamCode) && !string.IsNullOrWhiteSpace(definition.DefaultValue))
            {
                if (definition.ParamCode.Equals("isEnabled", StringComparison.OrdinalIgnoreCase))
                {
                    if (!bool.TryParse(definition.DefaultValue, out var defaultEnabled)) throw Invalid("Invalid default enabled filter.");
                    values[definition.ParamCode] = JsonSerializer.SerializeToElement(defaultEnabled);
                }
                else values[definition.ParamCode] = JsonSerializer.SerializeToElement(definition.DefaultValue);
            }
            if (definition.Required && (!values.TryGetValue(definition.ParamCode, out var required) || required.ValueKind == JsonValueKind.Null))
                throw Invalid("A required report filter is missing.");
        }
        string? StringValue(string key)
        {
            if (!values.TryGetValue(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind != JsonValueKind.String) throw Invalid("Invalid report filter type.");
            return value.GetString();
        }
        var keyword = StringValue("keyword")?.Trim();
        if (keyword?.Length > 100) throw Invalid("Report keyword is too long.");
        bool? enabled = null;
        if (values.TryGetValue("isEnabled", out var flag) && flag.ValueKind != JsonValueKind.Null)
            enabled = flag.ValueKind is JsonValueKind.True or JsonValueKind.False ? flag.GetBoolean() : throw Invalid("Invalid enabled filter.");
        var departmentText = StringValue("departmentId");
        Guid? departmentId = departmentText is null ? null :
            Guid.TryParse(departmentText, out var id) && id != Guid.Empty ? id : throw Invalid("Invalid department filter.");
        var departmentScope = StringValue("departmentScope") ?? "Authorized";
        if (departmentScope is not ("Authorized" or "CurrentDepartment")) throw Invalid("Invalid department scope.");
        var start = ParseTime(StringValue("startTime"));
        var end = ParseTime(StringValue("endTime"));
        if (start.HasValue && end.HasValue && start >= end) throw Invalid("Report start must precede its exclusive end.");
        return new() { Keyword = string.IsNullOrEmpty(keyword) ? null : keyword, IsEnabled = enabled, DepartmentId = departmentId,
            DepartmentScope = departmentScope, StartTime = start, EndTime = end };
    }

    public static void ValidateParameterType(string code, string type)
    {
        var valid = code.ToLowerInvariant() switch
        {
            "isenabled" => type.Equals("bool", StringComparison.OrdinalIgnoreCase) || type.Equals("boolean", StringComparison.OrdinalIgnoreCase),
            "departmentid" => type.Equals("guid", StringComparison.OrdinalIgnoreCase) || type.Equals("uuid", StringComparison.OrdinalIgnoreCase),
            "starttime" or "endtime" => type.Equals("datetime", StringComparison.OrdinalIgnoreCase) || type.Equals("datetimeoffset", StringComparison.OrdinalIgnoreCase),
            _ => type.Equals("string", StringComparison.OrdinalIgnoreCase)
        };
        if (!valid) throw Invalid("The controlled report parameter type does not match its contract.");
    }

    private static DateTimeOffset? ParseTime(string? text)
    {
        if (text is null) return null;
        if (text.Length > 40 || !Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant) ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            throw Invalid("Report time requires an ISO timestamp with explicit UTC offset.");
        return value.ToUniversalTime();
    }

    private static BusinessException Invalid(string message) => new(ErrorCode.ValidationFailed, message);
}

public sealed class ReportExecutionContext
{
    public Guid TenantId { get; init; }
    public Guid ActorUserId { get; init; }
    public DataScopeContext Scope { get; init; } = new();
}

public sealed class ReportUserFilters
{
    public string? Keyword { get; init; }
    public bool? IsEnabled { get; init; }
    public Guid? DepartmentId { get; init; }
    public string DepartmentScope { get; init; } = "Authorized";
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
}

public sealed record ReportMetricDefinition(string Code, string Name, string Definition, string Unit);
public sealed record ReportUserMetricValues(long UserCount, long EnabledUserCount, long DisabledUserCount);
public sealed record ReportUserMetricGroup(string? Key, ReportUserMetricValues Values);

public sealed class ReportUserMetrics
{
    public string Dimension { get; init; } = "None";
    public IReadOnlyList<ReportMetricDefinition> Definitions => ReportDatasetCapabilities.MetricDefinitions;
    public ReportUserMetricValues Totals { get; init; } = new(0, 0, 0);
    public long TotalGroupCount { get; init; }
    public List<ReportUserMetricGroup> Groups { get; init; } = [];
    public int DisplayedGroupCount => Groups.Count;
    public bool IsTruncated => TotalGroupCount > DisplayedGroupCount;
}
