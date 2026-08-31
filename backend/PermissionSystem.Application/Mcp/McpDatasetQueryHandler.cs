using System.Text.Json;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Mcp;

public interface IMcpDatasetQueryHandler
{
    string HandlerCode { get; }

    Task<McpDatasetQueryResponse> QueryAsync(
        McpDatasetQueryContext context,
        CancellationToken cancellationToken = default);
}

public interface IMcpDatasetQueryHandlerResolver
{
    IMcpDatasetQueryHandler GetRequired(string handlerCode);
}

public sealed class McpDatasetQueryHandlerResolver : IMcpDatasetQueryHandlerResolver
{
    private readonly IReadOnlyDictionary<string, IMcpDatasetQueryHandler> _handlers;

    public McpDatasetQueryHandlerResolver(IEnumerable<IMcpDatasetQueryHandler> handlers)
    {
        var registered = handlers.ToArray();
        var duplicate = registered
            .GroupBy(handler => handler.HandlerCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate MCP dataset handler '{duplicate.Key}'.");
        }

        _handlers = registered.ToDictionary(
            handler => handler.HandlerCode,
            StringComparer.OrdinalIgnoreCase);
    }

    public IMcpDatasetQueryHandler GetRequired(string handlerCode)
    {
        if (string.IsNullOrWhiteSpace(handlerCode) ||
            !_handlers.TryGetValue(handlerCode.Trim(), out var handler))
        {
            throw new BusinessException(ErrorCode.Conflict, "The MCP dataset handler is unavailable.");
        }

        return handler;
    }
}

public sealed class McpDatasetQueryContext
{
    public required Guid TenantId { get; init; }

    public required McpDatasetDefinition Dataset { get; init; }

    public required IReadOnlyList<McpDatasetField> SelectedFields { get; init; }

    public required IReadOnlyDictionary<string, JsonElement> Filters { get; init; }

    public required int Limit { get; init; }

    public required string TraceId { get; init; }

    public bool TryGetStringFilter(string key, out string value)
    {
        var match = Filters.FirstOrDefault(filter =>
            string.Equals(filter.Key, key, StringComparison.OrdinalIgnoreCase));
        value = match.Value.ValueKind == JsonValueKind.String
            ? match.Value.GetString()?.Trim() ?? string.Empty
            : string.Empty;
        if (value.Length > 100)
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "Dataset filter values cannot exceed 100 characters.");
        }

        return !string.IsNullOrWhiteSpace(value);
    }

    public bool TryGetBooleanFilter(string key, out bool value)
    {
        var match = Filters.FirstOrDefault(filter =>
            string.Equals(filter.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = match.Value.GetBoolean();
            return true;
        }

        value = default;
        return false;
    }

    public IReadOnlyDictionary<string, object?> ProjectRow(Func<string, object?> valueSelector)
    {
        return SelectedFields.ToDictionary(
            field => field.FieldCode,
            field => valueSelector(field.FieldCode),
            StringComparer.OrdinalIgnoreCase);
    }

    public McpDatasetQueryResponse CreateResponse(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        bool isTruncated)
    {
        return new McpDatasetQueryResponse
        {
            DatasetCode = Dataset.DatasetCode,
            DatasetVersion = Dataset.Version,
            SchemaHash = Dataset.SchemaHash,
            Fields = SelectedFields.Select(field => field.FieldCode).ToList(),
            Rows = rows,
            RowCount = rows.Count,
            IsTruncated = isTruncated,
            QueriedAt = DateTimeOffset.UtcNow,
            TraceId = TraceId
        };
    }
}
