using System.Text.Json;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Reports;

public sealed class ReportDefinitionQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? Category { get; init; }

    public string? DataSourceType { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreateReportDefinitionRequest
{
    public string ReportCode { get; init; } = string.Empty;

    public string ReportName { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string DataSourceType { get; init; } = "Sql";

    public string? DatasetKey { get; init; }

    public string? SqlText { get; init; }

    public string? ApiUrl { get; init; }

    public string? ColumnsJson { get; init; }

    public string? ParamsJson { get; init; }

    public bool IsEnabled { get; init; } = true;

    public string? Remark { get; init; }

    public IReadOnlyList<ReportQueryParamRequest> QueryParams { get; init; } = [];
}

public sealed class UpdateReportDefinitionRequest
{
    public string ReportName { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string DataSourceType { get; init; } = "Sql";

    public string? DatasetKey { get; init; }

    public string? SqlText { get; init; }

    public string? ApiUrl { get; init; }

    public string? ColumnsJson { get; init; }

    public string? ParamsJson { get; init; }

    public bool IsEnabled { get; init; } = true;

    public string? Remark { get; init; }

    public IReadOnlyList<ReportQueryParamRequest> QueryParams { get; init; } = [];
}

public sealed class ReportQueryParamRequest
{
    public string ParamCode { get; init; } = string.Empty;

    public string ParamName { get; init; } = string.Empty;

    public string ParamType { get; init; } = "String";

    public string? DefaultValue { get; init; }

    public bool Required { get; init; }

    public int Sort { get; init; }
}

public sealed class ReportDefinitionResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string ReportCode { get; init; } = string.Empty;

    public string ReportName { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string DataSourceType { get; init; } = string.Empty;

    public string? DatasetKey { get; init; }

    public string? ApiUrl { get; init; }

    public string? ColumnsJson { get; init; }

    public string? ParamsJson { get; init; }

    public bool IsEnabled { get; init; }

    public string? Remark { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public IReadOnlyList<ReportQueryParamResponse> QueryParams { get; init; } = [];
}

public sealed class ReportQueryParamResponse
{
    public Guid Id { get; init; }

    public Guid ReportId { get; init; }

    public string ParamCode { get; init; } = string.Empty;

    public string ParamName { get; init; } = string.Empty;

    public string ParamType { get; init; } = string.Empty;

    public string? DefaultValue { get; init; }

    public bool Required { get; init; }

    public int Sort { get; init; }
}

public sealed class ReportQueryRequest
{
    public Dictionary<string, JsonElement> Params { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ReportQueryResponse
{
    public IReadOnlyList<ReportColumnResponse> Columns { get; init; } = [];

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];

    public long ElapsedMilliseconds { get; init; }

    public int RowCount { get; init; }

}

public sealed class ReportColumnResponse
{
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Width { get; init; }

    public string? Type { get; init; }
}

public sealed class ReportExecutionLogQueryRequest : PaginationRequest
{
    public string? ReportCode { get; init; }

    public string? ExecuteUserName { get; init; }
}

public sealed class ReportExecutionLogResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid ReportId { get; init; }

    public string ReportCode { get; init; } = string.Empty;

    public Guid? ExecuteUserId { get; init; }

    public string? ExecuteUserName { get; init; }

    public string? ParamsJson { get; init; }

    public long ElapsedMilliseconds { get; init; }

    public int RowCount { get; init; }

    public bool IsSuccess { get; init; }

    public string? FailureReason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ReportExecutionRequest
{
    public string ReportCode { get; init; } = string.Empty;

    public string DataSourceType { get; init; } = string.Empty;

    public string? DatasetKey { get; init; }

    public string? ApiUrl { get; init; }

    public IReadOnlyList<ReportQueryParamResponse> QueryParams { get; init; } = [];

    public IReadOnlyDictionary<string, JsonElement> Params { get; init; } =
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ReportExecutionResult
{
    public IReadOnlyList<string> FieldNames { get; init; } = [];

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];

    public long ElapsedMilliseconds { get; init; }
}

public sealed class ReportDatasetResponse
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

public interface IReportService
{
    Task<PagedResult<ReportDefinitionResponse>> GetPagedAsync(ReportDefinitionQueryRequest request, CancellationToken cancellationToken = default);

    Task<ReportDefinitionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ReportDefinitionResponse> CreateAsync(CreateReportDefinitionRequest request, CancellationToken cancellationToken = default);

    Task<ReportDefinitionResponse> UpdateAsync(Guid id, UpdateReportDefinitionRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportDatasetResponse>> GetDatasetsAsync(CancellationToken cancellationToken = default);

    Task<ReportQueryResponse> QueryAsync(Guid id, ReportQueryRequest request, CancellationToken cancellationToken = default);

    Task<byte[]> ExportAsync(Guid id, ReportQueryRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<ReportExecutionLogResponse>> GetExecutionLogsAsync(
        ReportExecutionLogQueryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IReportQueryExecutor
{
    Task<ReportExecutionResult> ExecuteAsync(ReportExecutionRequest request, CancellationToken cancellationToken = default);
}

public interface IReportDatasetCatalog
{
    IReadOnlyList<ReportDatasetResponse> GetAvailable();

    ReportDatasetDefinition GetRequired(string datasetKey);
}

public sealed class ReportDatasetDefinition
{
    public string Key { get; init; } = string.Empty;

    public IReadOnlySet<string> FilterParameterCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
