using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiTools;

public abstract class AiReadOnlyToolHandlerBase<TArguments> : IAiReadOnlyToolHandler
    where TArguments : class, new()
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public abstract AiToolDefinition Definition { get; }

    public virtual bool IsEnabled => true;

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        if (context.TenantId == Guid.Empty || context.ActorUserId == Guid.Empty)
        {
            throw new BusinessException(ErrorCode.Forbidden, "The AI tool execution context is invalid.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var normalizedArguments = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson.Trim();
        TArguments arguments;
        try
        {
            using var document = JsonDocument.Parse(normalizedArguments);
            ValidateUniqueProperties(document.RootElement);
            arguments = JsonSerializer.Deserialize<TArguments>(normalizedArguments, JsonOptions)
                ?? throw new BusinessException(ErrorCode.ValidationFailed, "AI tool arguments are required.");
        }
        catch (JsonException exception)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI tool arguments are invalid.", exception);
        }

        return await ExecuteCoreAsync(context, arguments, normalizedArguments, cancellationToken);
    }

    private static void ValidateUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate tool argument.");
                ValidateUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) ValidateUniqueProperties(item);
    }

    protected abstract Task<AiToolExecutionResult> ExecuteCoreAsync(
        AiToolExecutionContext context,
        TArguments arguments,
        string rawArguments,
        CancellationToken cancellationToken);

    protected AiToolExecutionResult CreateResult(
        string rawArguments,
        object data,
        int rowCount,
        bool isTruncated,
        string? datasetCode = null,
        string? datasetVersion = null,
        PermissionSystem.Application.Permissions.PermissionDiagnosticResponse? permissionDiagnostic = null,
        AiQueryContext? queryContext = null, string? evaluationBasis = null,
        AiUserTableData? table = null, AiStatisticsData? statistics = null,
        PermissionSystem.Application.Reports.ReportUserMetrics? metrics = null, AiReportResultMetadata? report = null,
        DateTimeOffset? queriedAtOverride = null)
    {
        var queriedAt = queriedAtOverride ?? DateTimeOffset.UtcNow;
        var citation = new AiToolCitation
            {
                ToolCode = Definition.ToolCode,
                ToolVersion = Definition.Version,
                DatasetCode = datasetCode,
                DatasetVersion = datasetVersion,
                QueryParametersDigest = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(rawArguments))),
                QueriedAt = queriedAt,
                AsOf = report is null ? queriedAt : null,
                RowCount = rowCount
            };
        return new AiToolExecutionResult
        {
            PermissionDiagnostic = permissionDiagnostic,
            ContentJson = JsonSerializer.Serialize(data, JsonOptions),
            RowCount = rowCount,
            IsTruncated = isTruncated,
            Citation = citation,
            StructuredResult = queryContext is null ? null : new AiStructuredResult
            {
                Type = AiStructuredResults.ResultType(Definition.ToolCode),
                ToolCode = Definition.ToolCode, ToolVersion = Definition.Version,
                QueriedAt = queriedAt, EvaluationBasis = evaluationBasis ?? Definition.DataScopePolicy,
                Context = queryContext, Citation = citation, IsTruncated = isTruncated,
                Diagnostic = permissionDiagnostic, Table = table, Statistics = statistics, Metrics = metrics, Report = report,
                Limitations = report is null ? [] : [PermissionSystem.Application.Reports.ReportDatasetCapabilities.DataTimeLimitation]
            }
        };
    }

    protected static int ValidateLimit(int? requestedLimit, int maximumRows)
    {
        var limit = requestedLimit ?? Math.Min(20, maximumRows);
        if (limit is < 1 || limit > maximumRows)
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                $"Tool limit must be between 1 and {maximumRows}.");
        }

        return limit;
    }

    protected static string NormalizeKeyword(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length > 100)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Tool keyword is too long.");
        }

        return normalized;
    }

    protected static (DateTimeOffset StartTime, DateTimeOffset EndTime) NormalizeTimeRange(
        DateTimeOffset? requestedStart,
        DateTimeOffset? requestedEnd)
    {
        var endTime = requestedEnd ?? DateTimeOffset.UtcNow;
        var startTime = requestedStart ?? endTime.AddDays(-7);
        if (startTime > endTime || endTime - startTime > TimeSpan.FromDays(31))
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "Tool time range must be valid and no longer than 31 days.");
        }

        return (startTime, endTime);
    }
}

public class AiSearchArguments
{
    public string? Keyword { get; init; }

    public bool? IsEnabled { get; init; }

    public int? Limit { get; init; }
}

public sealed class AiUserSearchArguments : AiSearchArguments
{
    public string? DepartmentScope { get; init; }
}

public class AiLogSummaryArguments
{
    public string? UserName { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }
}

public sealed class AiOperationLogSummaryArguments : AiLogSummaryArguments
{
    public string? Module { get; init; }
}
