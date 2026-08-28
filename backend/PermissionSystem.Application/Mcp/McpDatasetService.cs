using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Mcp;

public sealed class McpDatasetService : IMcpDatasetService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMcpCallerContext _callerContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRepository<McpDatasetDefinition> _datasetRepository;
    private readonly IRepository<McpDatasetField> _fieldRepository;
    private readonly IRepository<McpClientDatasetGrant> _grantRepository;
    private readonly IRepository<McpInvocationLog> _logRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ITraceContextAccessor _traceContextAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public McpDatasetService(
        IMcpCallerContext callerContext,
        ICurrentUserService currentUserService,
        IRepository<McpDatasetDefinition> datasetRepository,
        IRepository<McpDatasetField> fieldRepository,
        IRepository<McpClientDatasetGrant> grantRepository,
        IRepository<McpInvocationLog> logRepository,
        IRepository<Department> departmentRepository,
        IAsyncQueryExecutor queryExecutor,
        ITraceContextAccessor traceContextAccessor,
        IUnitOfWork unitOfWork)
    {
        _callerContext = callerContext;
        _currentUserService = currentUserService;
        _datasetRepository = datasetRepository;
        _fieldRepository = fieldRepository;
        _grantRepository = grantRepository;
        _logRepository = logRepository;
        _departmentRepository = departmentRepository;
        _queryExecutor = queryExecutor;
        _traceContextAccessor = traceContextAccessor;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<McpDatasetResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAuditedAsync(
            "list_datasets",
            null,
            "{}",
            McpToolScopes.DatasetList,
            async token =>
            {
                var datasets = await _queryExecutor.ToListAsync(
                    _datasetRepository.QueryForTenant(_callerContext.TenantId)
                        .Where(entity => entity.IsEnabled)
                        .OrderBy(entity => entity.DatasetCode),
                    token);
                var result = new List<McpDatasetResponse>();
                foreach (var dataset in datasets)
                {
                    var access = await ResolveDatasetAccessAsync(dataset, token);
                    if (access is not null)
                    {
                        result.Add(ToDatasetResponse(dataset, access));
                    }
                }

                return ((IReadOnlyList<McpDatasetResponse>)result, result.Count, false);
            },
            cancellationToken);
    }

    public Task<McpDatasetResponse> DescribeAsync(
        string datasetCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeDatasetCode(datasetCode);
        return ExecuteAuditedAsync(
            "describe_dataset",
            normalizedCode,
            JsonSerializer.Serialize(new { datasetCode = normalizedCode }, JsonOptions),
            McpToolScopes.DatasetDescribe,
            async token =>
            {
                var (dataset, fields) = await GetAccessibleDatasetAsync(normalizedCode, token);
                return (ToDatasetResponse(dataset, fields), 1, false);
            },
            cancellationToken);
    }

    public Task<McpDatasetQueryResponse> QueryAsync(
        string datasetCode,
        McpDatasetQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeDatasetCode(datasetCode);
        var inputJson = JsonSerializer.Serialize(new
        {
            datasetCode = normalizedCode,
            request.Fields,
            request.Filters,
            request.Limit
        }, JsonOptions);
        return ExecuteAuditedAsync(
            "query_dataset",
            normalizedCode,
            inputJson,
            McpToolScopes.DatasetQuery,
            async token =>
            {
                var (dataset, accessibleFields) = await GetAccessibleDatasetAsync(normalizedCode, token);
                var selectedFields = ResolveSelectedFields(request.Fields, accessibleFields);
                ValidateFilters(request.Filters, accessibleFields);
                var limit = ResolveLimit(request.Limit, dataset.MaxRows);
                var result = dataset.HandlerCode switch
                {
                    McpDatasetCodes.PlatformCapabilities => QueryPlatformCapabilities(
                        dataset,
                        selectedFields,
                        request.Filters,
                        limit),
                    McpDatasetCodes.DepartmentDirectory => await QueryDepartmentsAsync(
                        dataset,
                        selectedFields,
                        request.Filters,
                        limit,
                        token),
                    _ => throw new BusinessException(ErrorCode.Conflict, "The MCP dataset handler is unavailable.")
                };
                return (result, result.RowCount, result.IsTruncated);
            },
            cancellationToken);
    }

    private async Task<T> ExecuteAuditedAsync<T>(
        string toolName,
        string? datasetCode,
        string inputJson,
        string requiredScope,
        Func<CancellationToken, Task<(T Result, int RowCount, bool IsTruncated)>> action,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            EnsureCallerAuthorized(requiredScope);
            var result = await action(cancellationToken);
            stopwatch.Stop();
            await RecordLogAsync(
                toolName,
                datasetCode,
                inputJson,
                McpInvocationStatus.Succeeded,
                result.RowCount,
                result.IsTruncated,
                startedAt,
                stopwatch.ElapsedMilliseconds,
                null,
                null,
                cancellationToken);
            return result.Result;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            var businessException = exception as BusinessException;
            try
            {
                using var auditCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await RecordLogAsync(
                    toolName,
                    datasetCode,
                    inputJson,
                    businessException?.ErrorCode is ErrorCode.Forbidden or ErrorCode.Unauthorized
                        ? McpInvocationStatus.Denied
                        : McpInvocationStatus.Failed,
                    0,
                    false,
                    startedAt,
                    stopwatch.ElapsedMilliseconds,
                    businessException?.ErrorCode.ToString() ?? ErrorCode.InternalServerError.ToString(),
                    Truncate(exception.Message, 1000),
                    auditCancellation.Token);
            }
            catch
            {
                // Preserve the original tool failure when best-effort audit persistence also fails.
            }
            if (exception is OperationCanceledException || businessException is not null)
            {
                throw;
            }

            throw new BusinessException(
                ErrorCode.InternalServerError,
                "The MCP dataset request could not be completed.");
        }
    }

    private void EnsureCallerAuthorized(string requiredScope)
    {
        if (!_callerContext.IsResolved || _callerContext.TenantId == Guid.Empty)
        {
            throw new BusinessException(ErrorCode.Unauthorized, "A resolved MCP caller is required.");
        }

        if (_callerContext.CallerType == McpCallerType.DelegatedUser)
        {
            if (!_currentUserService.IsAuthenticated ||
                _currentUserService.UserId != _callerContext.ActorUserId ||
                _currentUserService.TenantId != _callerContext.TenantId ||
                !_currentUserService.HasPermission(AiCenterConstants.McpDatasetQueryPermission))
            {
                throw new BusinessException(ErrorCode.Forbidden, "The delegated user cannot query MCP datasets.");
            }

            return;
        }

        if (!_callerContext.HasScope(requiredScope))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The MCP client is not allowed to use this tool.");
        }
    }

    private async Task<(McpDatasetDefinition Dataset, IReadOnlyList<McpDatasetField> Fields)> GetAccessibleDatasetAsync(
        string datasetCode,
        CancellationToken cancellationToken)
    {
        var dataset = await _queryExecutor.FirstOrDefaultAsync(
            _datasetRepository.QueryForTenant(_callerContext.TenantId).Where(entity =>
                entity.DatasetCode == datasetCode && entity.IsEnabled),
            cancellationToken) ?? throw new BusinessException(ErrorCode.NotFound, "The MCP dataset was not found.");
        var fields = await ResolveDatasetAccessAsync(dataset, cancellationToken);
        if (fields is null || fields.Count == 0)
        {
            throw new BusinessException(ErrorCode.Forbidden, "The MCP client is not authorized for this dataset.");
        }

        return (dataset, fields);
    }

    private async Task<IReadOnlyList<McpDatasetField>?> ResolveDatasetAccessAsync(
        McpDatasetDefinition dataset,
        CancellationToken cancellationToken)
    {
        var fields = await _queryExecutor.ToListAsync(
            _fieldRepository.QueryForTenant(_callerContext.TenantId)
                .Where(entity => entity.DatasetId == dataset.Id)
                .OrderBy(entity => entity.FieldCode),
            cancellationToken);
        if (_callerContext.CallerType == McpCallerType.DelegatedUser)
        {
            return fields;
        }

        if (!_callerContext.ClientBindingId.HasValue)
        {
            return null;
        }

        var grant = await _queryExecutor.FirstOrDefaultAsync(
            _grantRepository.QueryForTenant(_callerContext.TenantId).Where(entity =>
                entity.ClientBindingId == _callerContext.ClientBindingId.Value &&
                entity.DatasetId == dataset.Id &&
                entity.IsEnabled),
            cancellationToken);
        if (grant is null)
        {
            return null;
        }

        var allowedCodes = DeserializeAllowedFields(grant.AllowedFieldsJson);
        return fields.Where(field => allowedCodes.Contains(field.FieldCode, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private static IReadOnlyList<McpDatasetField> ResolveSelectedFields(
        IReadOnlyList<string> requestedFields,
        IReadOnlyList<McpDatasetField> accessibleFields)
    {
        var requested = requestedFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requested.Length == 0)
        {
            return accessibleFields.Where(field => field.IsDefault).ToList();
        }

        if (requested.Any(field =>
                accessibleFields.All(available => !string.Equals(available.FieldCode, field, StringComparison.OrdinalIgnoreCase))))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The requested dataset fields are not authorized.");
        }

        return requested
            .Select(field => accessibleFields.First(available =>
                string.Equals(available.FieldCode, field, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static void ValidateFilters(
        IReadOnlyDictionary<string, JsonElement> filters,
        IReadOnlyList<McpDatasetField> accessibleFields)
    {
        foreach (var filter in filters)
        {
            var field = accessibleFields.FirstOrDefault(candidate =>
                string.Equals(candidate.FieldCode, filter.Key, StringComparison.OrdinalIgnoreCase));
            if (field is null || !field.IsFilterable)
            {
                throw new BusinessException(ErrorCode.Forbidden, "The requested dataset filter is not authorized.");
            }

            if (field.DataType == "boolean" && filter.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "The dataset filter value has an invalid type.");
            }

            if (field.DataType == "string" && filter.Value.ValueKind != JsonValueKind.String)
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "The dataset filter value has an invalid type.");
            }
        }
    }

    private McpDatasetQueryResponse QueryPlatformCapabilities(
        McpDatasetDefinition dataset,
        IReadOnlyList<McpDatasetField> selectedFields,
        IReadOnlyDictionary<string, JsonElement> filters,
        int limit)
    {
        IEnumerable<PlatformCapabilityRow> query =
        [
            new("ai-chat", "Internal AI chat", "Enabled"),
            new("read-only-tools", "Permission-aware read-only tools", "Enabled"),
            new("document-drafts", "Controlled document drafts", "Enabled"),
            new("controlled-execution", "Confirmed business document execution", "Enabled"),
            new("external-mcp", "External MCP dataset access", "Enabled")
        ];
        query = ApplyStringFilter(query, filters, "code", row => row.Code);
        query = ApplyStringFilter(query, filters, "name", row => row.Name);
        query = ApplyStringFilter(query, filters, "status", row => row.Status);
        var rows = query.Take(limit + 1).ToList();
        return CreateQueryResponse(
            dataset,
            selectedFields,
            rows.Take(limit).Select(row => ProjectRow(selectedFields, field => field switch
            {
                "code" => row.Code,
                "name" => row.Name,
                "status" => row.Status,
                _ => null
            })).ToList(),
            rows.Count > limit);
    }

    private async Task<McpDatasetQueryResponse> QueryDepartmentsAsync(
        McpDatasetDefinition dataset,
        IReadOnlyList<McpDatasetField> selectedFields,
        IReadOnlyDictionary<string, JsonElement> filters,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = _departmentRepository.QueryForTenant(_callerContext.TenantId);
        if (TryGetStringFilter(filters, "code", out var code))
        {
            query = query.Where(entity => entity.Code.Contains(code));
        }

        if (TryGetStringFilter(filters, "name", out var name))
        {
            query = query.Where(entity => entity.Name.Contains(name));
        }

        if (TryGetBooleanFilter(filters, "isEnabled", out var isEnabled))
        {
            query = query.Where(entity => entity.IsEnabled == isEnabled);
        }

        var rows = await _queryExecutor.ToListAsync(
            query.OrderBy(entity => entity.Code)
                .Take(limit + 1)
                .Select(entity => new DepartmentRow(
                    entity.Code,
                    entity.Name,
                    entity.Parent == null ? null : entity.Parent.Code,
                    entity.IsEnabled)),
            cancellationToken);
        return CreateQueryResponse(
            dataset,
            selectedFields,
            rows.Take(limit).Select(row => ProjectRow(selectedFields, field => field switch
            {
                "code" => row.Code,
                "name" => row.Name,
                "parentCode" => row.ParentCode,
                "isEnabled" => row.IsEnabled,
                _ => null
            })).ToList(),
            rows.Count > limit);
    }

    private McpDatasetQueryResponse CreateQueryResponse(
        McpDatasetDefinition dataset,
        IReadOnlyList<McpDatasetField> selectedFields,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        bool isTruncated)
    {
        return new McpDatasetQueryResponse
        {
            DatasetCode = dataset.DatasetCode,
            DatasetVersion = dataset.Version,
            Fields = selectedFields.Select(field => field.FieldCode).ToList(),
            Rows = rows,
            RowCount = rows.Count,
            IsTruncated = isTruncated,
            QueriedAt = DateTimeOffset.UtcNow,
            TraceId = _traceContextAccessor.TraceId
        };
    }

    private static IReadOnlyDictionary<string, object?> ProjectRow(
        IReadOnlyList<McpDatasetField> fields,
        Func<string, object?> valueSelector)
    {
        return fields.ToDictionary(
            field => field.FieldCode,
            field => valueSelector(field.FieldCode),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task RecordLogAsync(
        string toolName,
        string? datasetCode,
        string inputJson,
        McpInvocationStatus status,
        int rowCount,
        bool isTruncated,
        DateTimeOffset startedAt,
        long durationMilliseconds,
        string? errorCode,
        string? errorSummary,
        CancellationToken cancellationToken)
    {
        await _logRepository.AddAsync(new McpInvocationLog
        {
            TenantId = _callerContext.TenantId,
            ClientBindingId = _callerContext.ClientBindingId,
            CallerType = _callerContext.CallerType,
            ActorUserId = _callerContext.ActorUserId,
            OAuthClientId = _callerContext.OAuthClientId,
            ToolName = toolName,
            DatasetCode = datasetCode,
            TraceId = _traceContextAccessor.TraceId,
            InputDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputJson))),
            IpAddress = _callerContext.IpAddress,
            Status = status,
            RowCount = rowCount,
            IsTruncated = isTruncated,
            StartedAt = startedAt,
            CompletedAt = startedAt.AddMilliseconds(durationMilliseconds),
            DurationMilliseconds = durationMilliseconds,
            ErrorCode = errorCode,
            ErrorSummary = errorSummary
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static McpDatasetResponse ToDatasetResponse(
        McpDatasetDefinition dataset,
        IReadOnlyList<McpDatasetField> fields) => new()
        {
            Id = dataset.Id,
            DatasetCode = dataset.DatasetCode,
            DatasetName = dataset.DatasetName,
            Version = dataset.Version,
            Description = dataset.Description,
            DataClassification = dataset.DataClassification,
            MaxRows = dataset.MaxRows,
            Fields = fields.Select(field => new McpDatasetFieldResponse
            {
                FieldCode = field.FieldCode,
                DisplayName = field.DisplayName,
                DataType = field.DataType,
                DataClassification = field.DataClassification,
                IsFilterable = field.IsFilterable,
                IsDefault = field.IsDefault
            }).ToList()
        };

    private static IReadOnlyList<string> DeserializeAllowedFields(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeDatasetCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Dataset code is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > 100)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Dataset code is too long.");
        }

        return normalized;
    }

    private static int ResolveLimit(int? requested, int maximum)
    {
        if (maximum is < 1 or > 200)
        {
            throw new BusinessException(ErrorCode.Conflict, "The dataset row limit is invalid.");
        }

        if (requested is <= 0)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Query limit must be greater than zero.");
        }

        return Math.Min(requested ?? maximum, maximum);
    }

    private static bool TryGetStringFilter(
        IReadOnlyDictionary<string, JsonElement> filters,
        string key,
        out string value)
    {
        var match = filters.FirstOrDefault(filter => string.Equals(filter.Key, key, StringComparison.OrdinalIgnoreCase));
        value = match.Value.ValueKind == JsonValueKind.String ? match.Value.GetString()?.Trim() ?? string.Empty : string.Empty;
        if (value.Length > 100)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Dataset filter values cannot exceed 100 characters.");
        }

        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetBooleanFilter(
        IReadOnlyDictionary<string, JsonElement> filters,
        string key,
        out bool value)
    {
        var match = filters.FirstOrDefault(filter => string.Equals(filter.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = match.Value.GetBoolean();
            return true;
        }

        value = default;
        return false;
    }

    private static IEnumerable<T> ApplyStringFilter<T>(
        IEnumerable<T> source,
        IReadOnlyDictionary<string, JsonElement> filters,
        string key,
        Func<T, string> selector)
    {
        return TryGetStringFilter(filters, key, out var value)
            ? source.Where(item => selector(item).Contains(value, StringComparison.OrdinalIgnoreCase))
            : source;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed record PlatformCapabilityRow(string Code, string Name, string Status);

    private sealed record DepartmentRow(string Code, string Name, string? ParentCode, bool IsEnabled);
}
