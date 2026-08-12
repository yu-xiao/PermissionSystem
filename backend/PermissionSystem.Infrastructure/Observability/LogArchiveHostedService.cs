using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.Infrastructure.Observability;

public sealed class LogArchiveHostedService : BackgroundService
{
    private readonly LogArchiveService _logArchiveService;
    private readonly LogArchiveOptions _options;
    private readonly ILogger<LogArchiveHostedService> _logger;

    public LogArchiveHostedService(
        LogArchiveService logArchiveService,
        IOptions<LogArchiveOptions> options,
        ILogger<LogArchiveHostedService> logger)
    {
        _logArchiveService = logArchiveService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _logArchiveService.ArchiveAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Log archive execution failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.CleanupIntervalMinutes)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
