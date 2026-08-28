namespace PermissionSystem.Application.AiTools;

public sealed class AiDatasetDescriptor
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string DataClassification { get; init; } = string.Empty;
}

public sealed class AiToolResult<T>
{
    public T Data { get; init; } = default!;

    public string Source { get; init; } = string.Empty;

    public DateTimeOffset QueriedAt { get; init; }

    public bool IsComplete { get; init; }

    public string? TraceId { get; init; }
}

public interface IAiToolService
{
    Task<AiToolResult<IReadOnlyList<AiDatasetDescriptor>>> ListDatasetsAsync(
        CancellationToken cancellationToken = default);
}

public interface IAiToolConfiguration
{
    bool EnableReportDatasetTool { get; }

    IReadOnlyCollection<string> ApprovedReportDatasetKeys { get; }

    int MaxToolRows { get; }
}

internal sealed class DefaultAiToolConfiguration : IAiToolConfiguration
{
    public bool EnableReportDatasetTool => false;

    public IReadOnlyCollection<string> ApprovedReportDatasetKeys => [];

    public int MaxToolRows => 200;
}

public sealed class AiToolDefinition
{
    public string ToolCode { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string InputSchemaJson { get; init; } = string.Empty;

    public string DataClassification { get; init; } = string.Empty;
}

public sealed class AiToolCitation
{
    public string SourceSystem { get; init; } = "PermissionSystem";

    public string ToolCode { get; init; } = string.Empty;

    public string ToolVersion { get; init; } = string.Empty;

    public string? DatasetCode { get; init; }

    public string? DatasetVersion { get; init; }

    public string QueryParametersDigest { get; init; } = string.Empty;

    public DateTimeOffset QueriedAt { get; init; }

    public DateTimeOffset? AsOf { get; init; }

    public int RowCount { get; init; }
}

public sealed class AiToolExecutionResult
{
    public string ContentJson { get; init; } = string.Empty;

    public int RowCount { get; init; }

    public bool IsTruncated { get; init; }

    public bool IncludeCitation { get; init; } = true;

    public AiToolCitation Citation { get; init; } = new();
}

public interface IAiReadOnlyToolRegistry
{
    IReadOnlyList<AiToolDefinition> GetAvailableTools();

    Task<AiToolExecutionResult> ExecuteAsync(
        string toolCode,
        string argumentsJson,
        CancellationToken cancellationToken = default);
}
