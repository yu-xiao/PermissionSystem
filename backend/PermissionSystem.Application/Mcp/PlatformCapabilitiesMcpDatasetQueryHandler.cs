namespace PermissionSystem.Application.Mcp;

public sealed class PlatformCapabilitiesMcpDatasetQueryHandler : IMcpDatasetQueryHandler
{
    public string HandlerCode => McpDatasetCodes.PlatformCapabilities;

    public Task<McpDatasetQueryResponse> QueryAsync(
        McpDatasetQueryContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<PlatformCapabilityRow> query =
        [
            new("ai-chat", "Internal AI chat", "Enabled"),
            new("read-only-tools", "Permission-aware read-only tools", "Enabled"),
            new("document-drafts", "Controlled document drafts", "Enabled"),
            new("controlled-execution", "Confirmed business document execution", "Enabled"),
            new("external-mcp", "External MCP dataset access", "Enabled")
        ];
        query = ApplyStringFilter(query, context, "code", row => row.Code);
        query = ApplyStringFilter(query, context, "name", row => row.Name);
        query = ApplyStringFilter(query, context, "status", row => row.Status);
        var rows = query.Take(context.Limit + 1).ToList();
        var response = context.CreateResponse(
            rows.Take(context.Limit).Select(row => context.ProjectRow(field => field switch
            {
                "code" => row.Code,
                "name" => row.Name,
                "status" => row.Status,
                _ => null
            })).ToList(),
            rows.Count > context.Limit);
        return Task.FromResult(response);
    }

    private static IEnumerable<T> ApplyStringFilter<T>(
        IEnumerable<T> source,
        McpDatasetQueryContext context,
        string key,
        Func<T, string> selector)
    {
        return context.TryGetStringFilter(key, out var value)
            ? source.Where(item => selector(item).Contains(value, StringComparison.OrdinalIgnoreCase))
            : source;
    }

    private sealed record PlatformCapabilityRow(string Code, string Name, string Status);
}
