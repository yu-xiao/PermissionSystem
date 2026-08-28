using System.Collections.Concurrent;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiRunCancellationCoordinator
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeRuns = new();

    public AiRunCancellationLease Begin(Guid runId, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!_activeRuns.TryAdd(runId, source))
        {
            source.Dispose();
            throw new InvalidOperationException("The AI run is already active in this process.");
        }

        return new AiRunCancellationLease(runId, source, Remove);
    }

    public void RequestCancellation(Guid runId)
    {
        if (_activeRuns.TryGetValue(runId, out var source))
        {
            source.Cancel();
        }
    }

    private void Remove(Guid runId, CancellationTokenSource source)
    {
        _activeRuns.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(runId, source));
    }
}

public sealed class AiRunCancellationLease : IDisposable
{
    private readonly Guid _runId;
    private readonly CancellationTokenSource _source;
    private readonly Action<Guid, CancellationTokenSource> _release;
    private int _disposed;

    internal AiRunCancellationLease(
        Guid runId,
        CancellationTokenSource source,
        Action<Guid, CancellationTokenSource> release)
    {
        _runId = runId;
        _source = source;
        _release = release;
    }

    public CancellationToken Token => _source.Token;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _release(_runId, _source);
        _source.Dispose();
    }
}

public sealed class NullAiRunRealtimeSender : IAiRunRealtimeSender
{
    public Task SendToUserAsync(
        Guid userId,
        AiRunRealtimeMessage message,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
