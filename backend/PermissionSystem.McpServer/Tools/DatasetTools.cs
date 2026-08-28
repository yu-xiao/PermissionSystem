using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PermissionSystem.Application.Mcp;

namespace PermissionSystem.McpServer.Tools;

[McpServerToolType]
public sealed class DatasetTools
{
    private readonly IMcpDatasetService _datasetService;

    public DatasetTools(IMcpDatasetService datasetService)
    {
        _datasetService = datasetService;
    }

    [McpServerTool(Name = "list_datasets", UseStructuredContent = true)]
    [Description("Lists the non-sensitive datasets currently approved for AI tool use.")]
    public Task<IReadOnlyList<McpDatasetResponse>> ListDatasetsAsync(
        CancellationToken cancellationToken = default)
    {
        return _datasetService.ListAsync(cancellationToken);
    }

    [McpServerTool(Name = "describe_dataset", UseStructuredContent = true)]
    [Description("Describes an authorized dataset and the fields available to the current caller.")]
    public Task<McpDatasetResponse> DescribeDatasetAsync(
        string datasetCode,
        CancellationToken cancellationToken = default)
    {
        return _datasetService.DescribeAsync(datasetCode, cancellationToken);
    }

    [McpServerTool(Name = "query_dataset", UseStructuredContent = true)]
    [Description("Queries authorized fields from an approved tenant-scoped dataset.")]
    public Task<McpDatasetQueryResponse> QueryDatasetAsync(
        string datasetCode,
        IReadOnlyList<string>? fields = null,
        Dictionary<string, JsonElement>? filters = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return _datasetService.QueryAsync(
            datasetCode,
            new McpDatasetQueryRequest
            {
                Fields = fields ?? [],
                Filters = filters ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase),
                Limit = limit
            },
            cancellationToken);
    }
}
