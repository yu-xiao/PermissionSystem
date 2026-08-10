using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.Excels;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Reports;

public sealed class ReportService : IReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRepository<ReportDefinition> _definitionRepository;
    private readonly IRepository<ReportQueryParam> _paramRepository;
    private readonly IRepository<ReportExecutionLog> _logRepository;
    private readonly IReportQueryExecutor _queryExecutor;
    private readonly IExcelService _excelService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReportDatasetCatalog _datasetCatalog;
    private readonly IAsyncQueryExecutor _asyncQueryExecutor;

    public ReportService(
        IRepository<ReportDefinition> definitionRepository,
        IRepository<ReportQueryParam> paramRepository,
        IRepository<ReportExecutionLog> logRepository,
        IReportQueryExecutor queryExecutor,
        IExcelService excelService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IReportDatasetCatalog datasetCatalog,
        IAsyncQueryExecutor asyncQueryExecutor)
    {
        _definitionRepository = definitionRepository;
        _paramRepository = paramRepository;
        _logRepository = logRepository;
        _queryExecutor = queryExecutor;
        _excelService = excelService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _datasetCatalog = datasetCatalog;
        _asyncQueryExecutor = asyncQueryExecutor;
    }

    public async Task<PagedResult<ReportDefinitionResponse>> GetPagedAsync(
        ReportDefinitionQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _definitionRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.ReportCode.Contains(keyword) ||
                entity.ReportName.Contains(keyword) ||
                entity.Category.Contains(keyword) ||
                (entity.Remark != null && entity.Remark.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim();
            query = query.Where(entity => entity.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(request.DataSourceType))
        {
            var dataSourceType = request.DataSourceType.Trim();
            query = query.Where(entity => entity.DataSourceType == dataSourceType);
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var items = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderBy(entity => entity.Category)
                .ThenBy(entity => entity.ReportCode)
                .Skip(request.Skip)
                .Take(request.PageSize)
                .Select(entity => new ReportDefinitionResponse
                {
                    Id = entity.Id,
                    TenantId = entity.TenantId,
                    ReportCode = entity.ReportCode,
                    ReportName = entity.ReportName,
                    Category = entity.Category,
                    DataSourceType = entity.DataSourceType,
                    DatasetKey = entity.DatasetKey,
                    ApiUrl = entity.ApiUrl,
                    IsEnabled = entity.IsEnabled,
                    Remark = entity.Remark,
                    CreatedAt = entity.CreatedAt,
                    ConcurrencyToken = entity.RowVersion
                }),
            cancellationToken);

        return PagedResult<ReportDefinitionResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount);
    }

    public async Task<ReportDefinitionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        return ToResponse(definition, await GetParamsAsync(definition.Id, cancellationToken));
    }

    public async Task<ReportDefinitionResponse> CreateAsync(
        CreateReportDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var reportCode = TrimRequired(request.ReportCode, "Report code is required.");
        if (await _asyncQueryExecutor.AnyAsync(
                _definitionRepository.Query().Where(entity => entity.ReportCode == reportCode),
                cancellationToken))
        {
            throw new BusinessException(ErrorCode.Conflict, "Report code already exists.");
        }

        var dataSourceType = NormalizeDataSourceType(request.DataSourceType);
        var datasetKey = NormalizeDatasetKey(dataSourceType, request.DatasetKey, request.SqlText);
        ValidateQueryParams(dataSourceType, datasetKey, request.QueryParams);
        var definition = new ReportDefinition
        {
            ReportCode = reportCode,
            ReportName = TrimRequired(request.ReportName, "Report name is required."),
            Category = TrimRequired(request.Category, "Category is required."),
            DataSourceType = dataSourceType,
            DatasetKey = datasetKey,
            SqlText = null,
            ApiUrl = NormalizeOptional(request.ApiUrl),
            ColumnsJson = NormalizeJson(request.ColumnsJson, "Columns JSON is invalid."),
            ParamsJson = NormalizeJson(request.ParamsJson, "Params JSON is invalid."),
            IsEnabled = request.IsEnabled,
            Remark = NormalizeOptional(request.Remark)
        };

        await _definitionRepository.AddAsync(definition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SaveParamsAsync(definition.Id, request.QueryParams, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(definition, await GetParamsAsync(definition.Id, cancellationToken));
    }

    public async Task<ReportDefinitionResponse> UpdateAsync(
        Guid id,
        UpdateReportDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(definition, request.ConcurrencyToken);
        definition.ReportName = TrimRequired(request.ReportName, "Report name is required.");
        definition.Category = TrimRequired(request.Category, "Category is required.");
        var dataSourceType = NormalizeDataSourceType(request.DataSourceType);
        var datasetKey = NormalizeDatasetKey(dataSourceType, request.DatasetKey, request.SqlText);
        ValidateQueryParams(dataSourceType, datasetKey, request.QueryParams);
        definition.DataSourceType = dataSourceType;
        definition.DatasetKey = datasetKey;
        definition.SqlText = null;
        definition.ApiUrl = NormalizeOptional(request.ApiUrl);
        definition.ColumnsJson = NormalizeJson(request.ColumnsJson, "Columns JSON is invalid.");
        definition.ParamsJson = NormalizeJson(request.ParamsJson, "Params JSON is invalid.");
        definition.IsEnabled = request.IsEnabled;
        definition.Remark = NormalizeOptional(request.Remark);

        _definitionRepository.Update(definition);
        await SaveParamsAsync(definition.Id, request.QueryParams, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(definition, await GetParamsAsync(definition.Id, cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        var parameters = await _asyncQueryExecutor.ToListAsync(
            _paramRepository.Query().Where(entity => entity.ReportId == definition.Id),
            cancellationToken);
        foreach (var parameter in parameters)
        {
            _paramRepository.Remove(parameter);
        }

        _definitionRepository.Remove(definition);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<ReportDatasetResponse>> GetDatasetsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_datasetCatalog.GetAvailable());
    }

    public async Task<ReportQueryResponse> QueryAsync(
        Guid id,
        ReportQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        if (!definition.IsEnabled)
        {
            throw new BusinessException(ErrorCode.Conflict, "Report is disabled.");
        }

        var parameters = await GetParamsAsync(definition.Id, cancellationToken);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        ReportExecutionResult executionResult;
        try
        {
            executionResult = await _queryExecutor.ExecuteAsync(
                new ReportExecutionRequest
                {
                    ReportCode = definition.ReportCode,
                    DataSourceType = definition.DataSourceType,
                    DatasetKey = definition.DatasetKey,
                    ApiUrl = definition.ApiUrl,
                    QueryParams = parameters,
                    Params = request.Params
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            await WriteExecutionLogAsync(definition, request, new ReportExecutionResult { ElapsedMilliseconds = stopwatch.ElapsedMilliseconds }, false, "Report execution was cancelled.", CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            var failureReason = exception is BusinessException businessException
                ? businessException.Message
                : "Report execution failed.";
            await WriteExecutionLogAsync(definition, request, new ReportExecutionResult { ElapsedMilliseconds = stopwatch.ElapsedMilliseconds }, false, failureReason, CancellationToken.None);
            throw;
        }

        await WriteExecutionLogAsync(definition, request, executionResult, true, null, cancellationToken);
        var columns = ResolveColumns(definition.ColumnsJson, executionResult.FieldNames);
        return new ReportQueryResponse
        {
            Columns = columns,
            Rows = ProjectRows(executionResult.Rows, columns),
            ElapsedMilliseconds = executionResult.ElapsedMilliseconds,
            RowCount = executionResult.Rows.Count
        };
    }

    public async Task<byte[]> ExportAsync(
        Guid id,
        ReportQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync(id, request, cancellationToken);
        return await _excelService.ExportTableAsync(
            new ExportTableRequest
            {
                SheetName = "Report",
                Columns = result.Columns
                    .Select(column => new ExportTableColumn
                    {
                        Key = column.Key,
                        Header = column.Title
                    })
                    .ToList(),
                Rows = result.Rows
            },
            cancellationToken);
    }

    public async Task<PagedResult<ReportExecutionLogResponse>> GetExecutionLogsAsync(
        ReportExecutionLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _logRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.ReportCode))
        {
            var reportCode = request.ReportCode.Trim();
            query = query.Where(entity => entity.ReportCode.Contains(reportCode));
        }

        if (!string.IsNullOrWhiteSpace(request.ExecuteUserName))
        {
            var executeUserName = request.ExecuteUserName.Trim();
            query = query.Where(entity => entity.ExecuteUserName != null && entity.ExecuteUserName.Contains(executeUserName));
        }

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var items = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderByDescending(entity => entity.CreatedAt)
                .Skip(request.Skip)
                .Take(request.PageSize)
                .Select(entity => new ReportExecutionLogResponse
                {
                    Id = entity.Id,
                    TenantId = entity.TenantId,
                    ReportId = entity.ReportId,
                    ReportCode = entity.ReportCode,
                    ExecuteUserId = entity.ExecuteUserId,
                    ExecuteUserName = entity.ExecuteUserName,
                    ParamsJson = entity.ParamsJson,
                    ElapsedMilliseconds = entity.ElapsedMilliseconds,
                    RowCount = entity.RowCount,
                    IsSuccess = entity.IsSuccess,
                    FailureReason = entity.FailureReason,
                    CreatedAt = entity.CreatedAt
                }),
            cancellationToken);

        return PagedResult<ReportExecutionLogResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount);
    }

    private async Task SaveParamsAsync(
        Guid reportId,
        IReadOnlyList<ReportQueryParamRequest> requests,
        CancellationToken cancellationToken)
    {
        var existing = await _asyncQueryExecutor.ToListAsync(
            _paramRepository.Query().Where(entity => entity.ReportId == reportId),
            cancellationToken);
        var existingByCode = existing.ToDictionary(entity => entity.ParamCode, StringComparer.OrdinalIgnoreCase);
        var requestedCodes = requests
            .Where(item => !string.IsNullOrWhiteSpace(item.ParamCode))
            .Select(item => item.ParamCode.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in existing.Where(parameter => !requestedCodes.Contains(parameter.ParamCode)))
        {
            _paramRepository.Remove(parameter);
        }

        foreach (var request in requests
            .Where(item => !string.IsNullOrWhiteSpace(item.ParamCode))
            .OrderBy(item => item.Sort))
        {
            var paramCode = TrimRequired(request.ParamCode, "Parameter code is required.");
            if (existingByCode.TryGetValue(paramCode, out var existingParameter))
            {
                existingParameter.ParamName = TrimRequired(request.ParamName, "Parameter name is required.");
                existingParameter.ParamType = TrimRequired(request.ParamType, "Parameter type is required.");
                existingParameter.DefaultValue = NormalizeOptional(request.DefaultValue);
                existingParameter.Required = request.Required;
                existingParameter.Sort = request.Sort;
                _paramRepository.Update(existingParameter);
                continue;
            }

            await _paramRepository.AddAsync(new ReportQueryParam
            {
                ReportId = reportId,
                ParamCode = paramCode,
                ParamName = TrimRequired(request.ParamName, "Parameter name is required."),
                ParamType = TrimRequired(request.ParamType, "Parameter type is required."),
                DefaultValue = NormalizeOptional(request.DefaultValue),
                Required = request.Required,
                Sort = request.Sort
            }, cancellationToken);
        }
    }

    private async Task WriteExecutionLogAsync(
        ReportDefinition definition,
        ReportQueryRequest request,
        ReportExecutionResult executionResult,
        bool isSuccess,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        await _logRepository.AddAsync(new ReportExecutionLog
        {
            TenantId = definition.TenantId,
            ReportId = definition.Id,
            ReportCode = definition.ReportCode,
            ExecuteUserId = _currentUserService.UserId,
            ExecuteUserName = _currentUserService.Username,
            ParamsJson = JsonSerializer.Serialize(request.Params, JsonOptions),
            ElapsedMilliseconds = executionResult.ElapsedMilliseconds,
            RowCount = executionResult.Rows.Count,
            IsSuccess = isSuccess,
            FailureReason = failureReason
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Task<IReadOnlyList<ReportQueryParamResponse>> GetParamsAsync(
        Guid reportId,
        CancellationToken cancellationToken)
    {
        return _asyncQueryExecutor.ToListAsync(
            _paramRepository.Query()
                .Where(entity => entity.ReportId == reportId)
                .OrderBy(entity => entity.Sort)
                .ThenBy(entity => entity.ParamCode)
                .Select(entity => new ReportQueryParamResponse
                {
                    Id = entity.Id,
                    ReportId = entity.ReportId,
                    ParamCode = entity.ParamCode,
                    ParamName = entity.ParamName,
                    ParamType = entity.ParamType,
                    DefaultValue = entity.DefaultValue,
                    Required = entity.Required,
                    Sort = entity.Sort
                }),
            cancellationToken);
    }

    private async Task<ReportDefinition> GetDefinitionOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _definitionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Report definition was not found.");
    }

    private static IReadOnlyList<ReportColumnResponse> ResolveColumns(string? columnsJson, IReadOnlyList<string> fieldNames)
    {
        var configuredColumns = ParseColumns(columnsJson);
        if (configuredColumns.Count > 0)
        {
            return configuredColumns;
        }

        return fieldNames
            .Where(field => !string.Equals(field, "TenantId", StringComparison.OrdinalIgnoreCase))
            .Select(field => new ReportColumnResponse
            {
                Key = field,
                Title = field
            })
            .ToList();
    }

    private static IReadOnlyList<ReportColumnResponse> ParseColumns(string? columnsJson)
    {
        if (string.IsNullOrWhiteSpace(columnsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ReportColumnResponse>>(columnsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ProjectRows(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IReadOnlyList<ReportColumnResponse> columns)
    {
        if (columns.Count == 0)
        {
            return rows;
        }

        return rows
            .Select(row => columns.ToDictionary(
                column => column.Key,
                column => row.TryGetValue(column.Key, out var value) ? value : null,
                StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static string NormalizeDataSourceType(string value)
    {
        var dataSourceType = TrimRequired(value, "Data source type is required.");
        return dataSourceType.Equals("Sql", StringComparison.OrdinalIgnoreCase)
            ? "Sql"
            : dataSourceType.Equals("Api", StringComparison.OrdinalIgnoreCase)
                ? "Api"
                : throw new BusinessException(ErrorCode.ValidationFailed, "Only Sql and Api data source types are supported.");
    }

    private string? NormalizeDatasetKey(string dataSourceType, string? datasetKey, string? legacySqlText)
    {
        if (!dataSourceType.Equals("Sql", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(legacySqlText))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "SQL report definitions must reference a configured dataset, not SQL text.");
        }

        var key = TrimRequired(datasetKey, "A report dataset is required for SQL reports.");
        _datasetCatalog.GetRequired(key);
        return key;
    }

    private void ValidateQueryParams(
        string dataSourceType,
        string? datasetKey,
        IReadOnlyList<ReportQueryParamRequest> queryParams)
    {
        if (!dataSourceType.Equals("Sql", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var allowed = _datasetCatalog.GetRequired(datasetKey ?? string.Empty).FilterParameterCodes;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in queryParams)
        {
            var code = TrimRequired(parameter.ParamCode, "Parameter code is required.");
            if (!seen.Add(code) || !allowed.Contains(code))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, $"Report parameter {code} is not allowed by the selected dataset.");
            }
        }
    }

    private static string? NormalizeJson(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
            return value.Trim();
        }
        catch (JsonException)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }

    private static ReportDefinitionResponse ToResponse(
        ReportDefinition entity,
        IReadOnlyList<ReportQueryParamResponse> parameters)
    {
        return new ReportDefinitionResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ReportCode = entity.ReportCode,
            ReportName = entity.ReportName,
            Category = entity.Category,
            DataSourceType = entity.DataSourceType,
            DatasetKey = entity.DatasetKey,
            ApiUrl = entity.ApiUrl,
            ColumnsJson = entity.ColumnsJson,
            ParamsJson = entity.ParamsJson,
            IsEnabled = entity.IsEnabled,
            Remark = entity.Remark,
            CreatedAt = entity.CreatedAt,
            ConcurrencyToken = entity.RowVersion,
            QueryParams = parameters
        };
    }

    private static ReportQueryParamResponse ToResponse(ReportQueryParam entity)
    {
        return new ReportQueryParamResponse
        {
            Id = entity.Id,
            ReportId = entity.ReportId,
            ParamCode = entity.ParamCode,
            ParamName = entity.ParamName,
            ParamType = entity.ParamType,
            DefaultValue = entity.DefaultValue,
            Required = entity.Required,
            Sort = entity.Sort
        };
    }

    private static ReportExecutionLogResponse ToResponse(ReportExecutionLog entity)
    {
        return new ReportExecutionLogResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ReportId = entity.ReportId,
            ReportCode = entity.ReportCode,
            ExecuteUserId = entity.ExecuteUserId,
            ExecuteUserName = entity.ExecuteUserName,
            ParamsJson = entity.ParamsJson,
            ElapsedMilliseconds = entity.ElapsedMilliseconds,
            RowCount = entity.RowCount,
            IsSuccess = entity.IsSuccess,
            FailureReason = entity.FailureReason,
            CreatedAt = entity.CreatedAt
        };
    }

    private static string TrimRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
