using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiCenter;

public sealed record AiRunAdmissionRequest(
    Guid TenantId,
    Guid UserId,
    string AgentCode,
    Guid ProviderId,
    int EstimatedTokens);

public interface IAiRunAdmissionService
{
    Task<T> ExecuteAsync<T>(
        AiRunAdmissionRequest request,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default);
}

public sealed class AiRunAdmissionService : IAiRunAdmissionService
{
    private readonly IRepository<AiRun> _runRepository;
    private readonly IRepository<AiUsageLog> _usageRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly IDistributedRateLimitService _rateLimitService;
    private readonly IDistributedLock _distributedLock;
    private readonly IAiCenterConfiguration _configuration;

    public AiRunAdmissionService(
        IRepository<AiRun> runRepository,
        IRepository<AiUsageLog> usageRepository,
        IAsyncQueryExecutor queryExecutor,
        IDistributedRateLimitService rateLimitService,
        IDistributedLock distributedLock,
        IAiCenterConfiguration configuration)
    {
        _runRepository = runRepository;
        _usageRepository = usageRepository;
        _queryExecutor = queryExecutor;
        _rateLimitService = rateLimitService;
        _distributedLock = distributedLock;
        _configuration = configuration;
    }

    public async Task<T> ExecuteAsync<T>(
        AiRunAdmissionRequest request,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var partitions = new[]
        {
            ("tenant", request.TenantId.ToString("N")),
            ("user", request.UserId.ToString("N")),
            ("agent", $"{request.TenantId:N}:{request.AgentCode}"),
            ("provider", $"{request.TenantId:N}:{request.ProviderId:N}")
        };
        foreach (var (name, key) in partitions)
        {
            var result = await _rateLimitService.TryAcquireAsync(
                $"ai-request:{name}",
                key,
                _configuration.RequestLimitPerMinute,
                TimeSpan.FromMinutes(1),
                cancellationToken);
            if (!result.IsAcquired)
            {
                throw new BusinessException(ErrorCode.TooManyRequests, "The AI request rate limit was exceeded.");
            }
        }

        var lockKey = $"ai:concurrency:{request.TenantId:N}";
        return await _distributedLock.ExecuteWithLockAsync(
            lockKey,
            async token =>
            {
                var activeRuns = await _queryExecutor.ToListAsync(
                    _runRepository.QueryForTenant(request.TenantId).Where(run =>
                        run.Status == AiRunStatus.Pending || run.Status == AiRunStatus.Running),
                    token);
                var tenantActive = activeRuns.Count;
                var userActive = activeRuns.LongCount(run => run.ActorUserId == request.UserId);
                var agentActive = activeRuns.LongCount(run => run.AgentCode == request.AgentCode);
                var providerActive = activeRuns.LongCount(run => run.ProviderConfigId == request.ProviderId);
                if (tenantActive >= _configuration.ConcurrentRunLimit ||
                    userActive >= _configuration.ConcurrentRunLimit ||
                    agentActive >= _configuration.ConcurrentRunLimit ||
                    providerActive >= _configuration.ConcurrentRunLimit)
                {
                    throw new BusinessException(ErrorCode.TooManyRequests, "The AI concurrent run limit was exceeded.");
                }

                var since = DateTimeOffset.UtcNow.AddHours(-1);
                var usageRows = await _queryExecutor.ToListAsync(
                    _usageRepository.QueryForTenant(request.TenantId)
                        .Where(log => log.CreatedAt >= since &&
                            log.RunId != Guid.Empty &&
                            log.InputTokens.HasValue)
                        .Select(log => new
                        {
                            log.RunId,
                            Tokens = (long)(log.InputTokens ?? 0) + (log.OutputTokens ?? 0)
                        }),
                    token);
                var usageRunIds = usageRows.Select(row => row.RunId).Distinct().ToArray();
                IReadOnlyList<AiRun> usageRuns = usageRunIds.Length == 0
                    ? []
                    : await _queryExecutor.ToListAsync(
                        _runRepository.QueryForTenant(request.TenantId)
                            .Where(run => usageRunIds.Contains(run.Id)),
                        token);
                var runById = usageRuns.ToDictionary(run => run.Id);
                var tenantTokens = usageRows.Sum(row => row.Tokens);
                var userTokens = usageRows.Where(row => runById.TryGetValue(row.RunId, out var run) && run.ActorUserId == request.UserId).Sum(row => row.Tokens);
                var agentTokens = usageRows.Where(row => runById.TryGetValue(row.RunId, out var run) && run.AgentCode == request.AgentCode).Sum(row => row.Tokens);
                var providerTokens = usageRows.Where(row => runById.TryGetValue(row.RunId, out var run) && run.ProviderConfigId == request.ProviderId).Sum(row => row.Tokens);
                var estimatedTokens = Math.Max(0, request.EstimatedTokens);
                if (tenantTokens + estimatedTokens > _configuration.TokenLimitPerHour ||
                    userTokens + estimatedTokens > _configuration.TokenLimitPerHour ||
                    agentTokens + estimatedTokens > _configuration.TokenLimitPerHour ||
                    providerTokens + estimatedTokens > _configuration.TokenLimitPerHour)
                {
                    throw new BusinessException(ErrorCode.TooManyRequests, "The AI token quota was exceeded.");
                }

                return await action();
            },
            expiry: TimeSpan.FromSeconds(15),
            waitTime: TimeSpan.FromSeconds(5),
            cancellationToken: cancellationToken);
    }
}

internal sealed class AiRunAdmissionServicePlaceholder : IAiRunAdmissionService
{
    public Task<T> ExecuteAsync<T>(AiRunAdmissionRequest request, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return action();
    }
}
