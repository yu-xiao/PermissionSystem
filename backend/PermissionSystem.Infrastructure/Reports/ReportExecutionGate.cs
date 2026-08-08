using Microsoft.Extensions.Options;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Infrastructure.Reports;

public sealed class ReportExecutionGate
{
    private readonly SemaphoreSlim _semaphore;

    public ReportExecutionGate(IOptions<ReportOptions> options)
    {
        _semaphore = new SemaphoreSlim(Math.Clamp(options.Value.MaxConcurrentQueries, 1, 32));
    }

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        if (!await _semaphore.WaitAsync(0, cancellationToken))
        {
            throw new BusinessException(ErrorCode.TooManyRequests, "The maximum number of concurrent report queries has been reached.");
        }

        return new Releaser(_semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _released;

        public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _semaphore.Release();
            }
        }
    }
}
