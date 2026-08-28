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
