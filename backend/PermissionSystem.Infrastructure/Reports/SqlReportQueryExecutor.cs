using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Reports;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Infrastructure.Reports;

public sealed class SqlReportQueryExecutor : IReportQueryExecutor
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ReportOptions _options;

    public SqlReportQueryExecutor(
        AppDbContext dbContext,
        ITenantContext tenantContext,
        IOptions<ReportOptions> options)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _options = options.Value;
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

        var sql = BuildSql(ReportSqlSecurity.ValidateSelectSql(request.SqlText));
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = Math.Clamp(_options.QueryTimeoutSeconds, 1, 300);
            AddParameters(command, request);

            var stopwatch = Stopwatch.StartNew();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var fieldNames = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToList();
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
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private string BuildSql(string sql)
    {
        var maxRows = Math.Clamp(_options.MaxRows, 1, 10000);
        var tenantFilter = _tenantContext.TenantId.HasValue && !_tenantContext.IsTenantFilterDisabled
            ? " WHERE report_source.TenantId = @__TenantId"
            : string.Empty;

        return $"SELECT TOP ({maxRows}) * FROM ({sql}) AS report_source{tenantFilter}";
    }

    private void AddParameters(DbCommand command, ReportExecutionRequest request)
    {
        if (_tenantContext.TenantId.HasValue && !_tenantContext.IsTenantFilterDisabled)
        {
            AddParameter(command, "__TenantId", _tenantContext.TenantId.Value);
        }

        foreach (var parameter in request.QueryParams)
        {
            var value = ResolveParameterValue(parameter, request.Params);
            AddParameter(command, parameter.ParamCode, value);
        }
    }

    private static object? ResolveParameterValue(
        ReportQueryParamResponse parameter,
        IReadOnlyDictionary<string, JsonElement> requestParams)
    {
        if (requestParams.TryGetValue(parameter.ParamCode, out var jsonValue) &&
            jsonValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            return ConvertJsonValue(jsonValue, parameter.ParamType);
        }

        if (!string.IsNullOrWhiteSpace(parameter.DefaultValue))
        {
            return ConvertStringValue(parameter.DefaultValue, parameter.ParamType);
        }

        if (parameter.Required)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, $"Report parameter {parameter.ParamCode} is required.");
        }

        return DBNull.Value;
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
            var value => value
        };
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name.StartsWith("@", StringComparison.Ordinal) ? name : $"@{name}";
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
