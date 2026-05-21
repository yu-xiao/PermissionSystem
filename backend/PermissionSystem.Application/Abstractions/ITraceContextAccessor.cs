namespace PermissionSystem.Application.Abstractions;

public interface ITraceContextAccessor
{
    string TraceId { get; set; }
}

public sealed class TraceContextAccessor : ITraceContextAccessor
{
    private static readonly AsyncLocal<string?> CurrentTraceId = new();

    public string TraceId
    {
        get => CurrentTraceId.Value ?? string.Empty;
        set => CurrentTraceId.Value = value;
    }
}
