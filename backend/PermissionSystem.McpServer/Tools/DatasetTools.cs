using System.ComponentModel;
using ModelContextProtocol.Server;
using PermissionSystem.Application.AiTools;

namespace PermissionSystem.McpServer.Tools;

[McpServerToolType]
public sealed class DatasetTools
{
    private readonly IAiToolService _toolService;

    public DatasetTools(IAiToolService toolService)
    {
        _toolService = toolService;
    }

    [McpServerTool(Name = "list_datasets", UseStructuredContent = true)]
    [Description("Lists the non-sensitive datasets currently approved for AI tool use.")]
    public Task<AiToolResult<IReadOnlyList<AiDatasetDescriptor>>> ListDatasetsAsync(
        CancellationToken cancellationToken = default)
    {
        return _toolService.ListDatasetsAsync(cancellationToken);
    }
}
