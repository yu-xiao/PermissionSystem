namespace PermissionSystem.Application.AiCenter;

public sealed record AiCircuitTarget(string Kind, string Key)
{
    public override string ToString() => $"{Kind}:{Key}";
}

public interface IAiCircuitBreaker
{
    Task<bool> AllowAsync(AiCircuitTarget target, CancellationToken cancellationToken = default);

    Task RecordSuccessAsync(AiCircuitTarget target, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(AiCircuitTarget target, string errorCode, CancellationToken cancellationToken = default);
}

internal sealed class AllowAllAiCircuitBreaker : IAiCircuitBreaker
{
    public Task<bool> AllowAsync(AiCircuitTarget target, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task RecordSuccessAsync(AiCircuitTarget target, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RecordFailureAsync(AiCircuitTarget target, string errorCode, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
