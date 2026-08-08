using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Reports;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Infrastructure.Reports;

public sealed class SqlReportQueryExecutor : IReportQueryExecutor
{
    private readonly ITenantContext _tenantContext;
    private readonly ReportOptions _options;
    private readonly ReportDatasetCatalog _datasetCatalog;
    private readonly ReportExecutionGate _executionGate;

    public SqlReportQueryExecutor(
        ITenantContext tenantContext,
        IOptions<ReportOptions> options,
        ReportDatasetCatalog datasetCatalog,
        ReportExecutionGate executionGate)
    {
        _tenantContext = tenantContext;
        _options = options.Value;
        _datasetCatalog = datasetCatalog;
        _executionGate = executionGate;
    }

    public async Task<ReportExecutionResult> ExecuteAsync(
        ReportExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.DataSourceType.Equals("Sql", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Only SQL report execution is implemented.");
        }

        if (!_options.SqlReportsEnabled)
        {
            throw new BusinessException(ErrorCode.Conflict, "SQL report execution is disabled by configuration.");
        }

        if (string.IsNullOrWhiteSpace(_options.ReportConnection))
        {
            throw new BusinessException(ErrorCode.Conflict, "The isolated report connection is not configured.");
        }

        if (!_tenantContext.TenantId.HasValue)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "A tenant context is required to execute a report.");
        }

        var dataset = _datasetCatalog.GetExecutionDefinition(request.DatasetKey ?? string.Empty);
        using var lease = await _executionGate.EnterAsync(cancellationToken);
        await using var connection = new SqlConnection(_options.ReportConnection);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BuildSql(dataset, request.QueryParams, request.Params, command);
        command.CommandType = CommandType.Text;
        command.CommandTimeout = Math.Clamp(_options.QueryTimeoutSeconds, 1, 300);
        AddParameter(command, "__TenantId", _tenantContext.TenantId.Value);
        AddParameter(command, "__MaxRows", Math.Clamp(_options.MaxRows, 1, 10000));

        try
        {
            var stopwatch = Stopwatch.StartNew();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var fieldNames = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var rows = new List<IReadOnlyDictionary<string, object?>>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row[fieldNames[index]] = await reader.IsDBNullAsync(index, cancellationToken)
                        ? null
                        : reader.GetValue(index);
                }

                rows.Add(row);
            }

            stopwatch.Stop();
            return new ReportExecutionResult
            {
                FieldNames = fieldNames,
                Rows = rows,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (SqlException exception) when (exception.Number == -2)
        {
            throw new BusinessException(ErrorCode.Conflict, "The report query exceeded its execution time limit.", exception);
        }
    }

    private static string BuildSql(
        ReportDatasetOptions dataset,
        IReadOnlyList<ReportQueryParamResponse> queryParams,
        IReadOnlyDictionary<string, JsonElement> requestParams,
        SqlCommand command)
    {
        var parameters = queryParams.ToDictionary(parameter => parameter.ParamCode, StringComparer.OrdinalIgnoreCase);
        var predicates = new List<string> { "report_source.[TenantId] = @__TenantId" };
        foreach (var filter in dataset.Filters)
        {
            if (!parameters.TryGetValue(filter.ParamCode, out var parameter) ||
                !TryResolveParameterValue(parameter, requestParams, out var value))
            {
                continue;
            }

            var parameterName = $"report_{filter.ParamCode}";
            AddParameter(command, parameterName, value);
            predicates.Add($"report_source.[{filter.ColumnName}] {GetOperator(filter.Operator)} @{parameterName}");
        }

        return $"SELECT TOP (@__MaxRows) * FROM {QuoteViewName(dataset.ViewName)} AS report_source WHERE {string.Join(" AND ", predicates)}";
    }

    private static bool TryResolveParameterValue(
        ReportQueryParamResponse parameter,
        IReadOnlyDictionary<string, JsonElement> requestParams,
        out object? value)
    {
        if (requestParams.TryGetValue(parameter.ParamCode, out var jsonValue) &&
            jsonValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            value = ConvertJsonValue(jsonValue, parameter.ParamType);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(parameter.DefaultValue))
        {
            value = ConvertStringValue(parameter.DefaultValue, parameter.ParamType);
            return true;
        }

        if (parameter.Required)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, $"Report parameter {parameter.ParamCode} is required.");
        }

        value = null;
        return false;
    }

    private static string QuoteViewName(string value)
    {
        var parts = value.Split('.', StringSplitOptions.TrimEntries);
        return $"[{parts[0]}].[{parts[1]}]";
    }

    private static string GetOperator(string value)
    {
        return value switch
        {
            "Equal" => "=",
            "GreaterThanOrEqual" => ">=",
            "LessThanOrEqual" => "<=",
            _ => throw new InvalidOperationException("Unsupported report dataset filter operator.")
        };
    }

    private static object? ConvertJsonValue(JsonElement value, string type)
    {
        return NormalizeType(type) switch
        {
            "int" => value.ValueKind == JsonValueKind.Number ? value.GetInt32() : int.Parse(value.GetString() ?? string.Empty, CultureInfo.InvariantCulture),
            "long" => value.ValueKind == JsonValueKind.Number ? value.GetInt64() : long.Parse(value.GetString() ?? string.Empty, CultureInfo.InvariantCulture),
            "decimal" => value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : decimal.Parse(value.GetString() ?? string.Empty, CultureInfo.InvariantCulture),
            "datetime" => value.ValueKind == JsonValueKind.String ? DateTimeOffset.Parse(value.GetString() ?? string.Empty, CultureInfo.InvariantCulture) : value.GetDateTimeOffset(),
            "bool" => value.ValueKind == JsonValueKind.True || (value.ValueKind != JsonValueKind.False && bool.Parse(value.GetString() ?? "false")),
            "guid" => Guid.Parse(value.GetString() ?? string.Empty),
            _ => value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
        };
    }

    private static object? ConvertStringValue(string value, string type)
    {
        return NormalizeType(type) switch
        {
            "int" => int.Parse(value, CultureInfo.InvariantCulture),
            "long" => long.Parse(value, CultureInfo.InvariantCulture),
            "decimal" => decimal.Parse(value, CultureInfo.InvariantCulture),
            "datetime" => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture),
            "bool" => bool.Parse(value),
            "guid" => Guid.Parse(value),
            _ => value
        };
    }

    private static string NormalizeType(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "number" or "integer" or "int32" => "int",
            "int64" => "long",
            "date" or "datetimeoffset" => "datetime",
            "boolean" => "bool",
            "uuid" => "guid",
            var normalized => normalized
        };
    }

    private static void AddParameter(SqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }
}
