using System.Text.RegularExpressions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiBudgetService : IAiBudgetService
{
    private static readonly Regex CodeRegex = new("^[a-z0-9][a-z0-9_-]{0,99}$", RegexOptions.Compiled);
    private static readonly Regex CurrencyRegex = new("^[A-Z]{3}$", RegexOptions.Compiled);
    private readonly IRepository<AiBudgetPolicy> _policyRepository;
    private readonly IRepository<AiUsageLog> _usageRepository;
    private readonly IRepository<AiRun> _runRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IDistributedLock _distributedLock;
    private readonly IUnitOfWork _unitOfWork;

    public AiBudgetService(
        IRepository<AiBudgetPolicy> policyRepository,
        IRepository<AiUsageLog> usageRepository,
        IRepository<AiRun> runRepository,
        IRepository<User> userRepository,
        IAsyncQueryExecutor queryExecutor,
        ITenantWriteResolver tenantWriteResolver,
        IDistributedLock distributedLock,
        IUnitOfWork unitOfWork)
    {
        _policyRepository = policyRepository;
        _usageRepository = usageRepository;
        _runRepository = runRepository;
        _userRepository = userRepository;
        _queryExecutor = queryExecutor;
        _tenantWriteResolver = tenantWriteResolver;
        _distributedLock = distributedLock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<AiBudgetPolicyResponse>> GetPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        var policies = await _queryExecutor.ToListAsync(
            _policyRepository.Query().OrderBy(entity => entity.ScopeType).ThenBy(entity => entity.PolicyCode),
            cancellationToken);
        if (policies.Count == 0)
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var currencies = policies.Select(entity => entity.Currency).Distinct().ToList();
        var usageLogs = await _queryExecutor.ToListAsync(
            _usageRepository.Query().Where(entity =>
                entity.CreatedAt >= monthStart &&
                currencies.Contains(entity.PricingCurrency!) &&
                (entity.EstimatedCost.HasValue ||
                    (entity.ReservedCost.HasValue && entity.ReservationExpiresAt > now))),
            cancellationToken);
        var runIds = usageLogs.Select(entity => entity.RunId).Distinct().ToList();
        var runActors = (await _queryExecutor.ToListAsync(
                _runRepository.Query().Where(entity => runIds.Contains(entity.Id)),
                cancellationToken))
            .ToDictionary(entity => entity.Id, entity => entity.ActorUserId);
        return policies.Select(policy =>
        {
            var current = usageLogs
                .Where(log => log.PricingCurrency == policy.Currency &&
                    (policy.ScopeType == AiBudgetScopeType.Tenant ||
                     (runActors.TryGetValue(log.RunId, out var actorId) && actorId == policy.UserId)))
                .Sum(log => log.EstimatedCost ?? log.ReservedCost ?? 0m);
            return ToResponse(policy, current);
        }).ToList();
    }

    public async Task<AiBudgetPolicyResponse> SavePolicyAsync(
        SaveAiBudgetPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        var code = request.PolicyCode?.Trim().ToLowerInvariant() ?? string.Empty;
        var currency = request.Currency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!CodeRegex.IsMatch(code) || !CurrencyRegex.IsMatch(currency) ||
            request.MonthlyLimit is <= 0 or > 999_999_999_999m ||
            request.AlertThresholdPercentage is < 1 or > 100)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI budget policy settings are invalid.");
        }

        if (request.ScopeType == AiBudgetScopeType.User)
        {
            if (!request.UserId.HasValue || !await _queryExecutor.AnyAsync(
                    _userRepository.QueryForTenant(tenantId).Where(entity => entity.Id == request.UserId.Value),
                    cancellationToken))
            {
                throw new BusinessException(ErrorCode.NotFound, "The budget user was not found in the selected tenant.");
            }
        }
        else if (request.UserId.HasValue)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Tenant budget policies cannot specify a user.");
        }

        var duplicateScope = await _queryExecutor.FirstOrDefaultAsync(
            _policyRepository.QueryForTenant(tenantId).Where(entity =>
                entity.PolicyCode != code &&
                entity.ScopeType == request.ScopeType &&
                entity.UserId == (request.ScopeType == AiBudgetScopeType.User ? request.UserId : null) &&
                entity.Currency == currency),
            cancellationToken);
        if (duplicateScope is not null)
        {
            throw new BusinessException(
                ErrorCode.Conflict,
                "Only one AI budget policy is allowed for the same scope and currency.");
        }

        var policy = await _queryExecutor.FirstOrDefaultAsync(
            _policyRepository.QueryForTenant(tenantId).Where(entity => entity.PolicyCode == code),
            cancellationToken);
        if (policy is null)
        {
            policy = new AiBudgetPolicy { TenantId = tenantId, PolicyCode = code };
            await _policyRepository.AddAsync(policy, cancellationToken);
        }
        else
        {
            ConcurrencyTokenGuard.EnsureMatches(policy, request.ConcurrencyToken);
            _policyRepository.Update(policy);
        }

        policy.PolicyName = NormalizeRequired(request.PolicyName, 200);
        policy.ScopeType = request.ScopeType;
        policy.UserId = request.ScopeType == AiBudgetScopeType.User ? request.UserId : null;
        policy.MonthlyLimit = request.MonthlyLimit;
        policy.Currency = currency;
        policy.IsHardLimit = request.IsHardLimit;
        policy.AlertThresholdPercentage = request.AlertThresholdPercentage;
        policy.IsEnabled = request.IsEnabled;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(policy, 0m);
    }

    public async Task ReserveInvocationAsync(
        AiUsageLog usage,
        AiProviderConfig provider,
        Guid userId,
        int estimatedInputTokens,
        int maxOutputTokens,
        CancellationToken cancellationToken = default)
    {
        usage.InputTokenPricePerMillion = provider.InputTokenPricePerMillion;
        usage.OutputTokenPricePerMillion = provider.OutputTokenPricePerMillion;
        usage.PricingCurrency = provider.PricingCurrency;
        if (!HasPricing(provider))
        {
            var hasHardBudget = await _queryExecutor.AnyAsync(
                _policyRepository.QueryForTenant(usage.TenantId).Where(entity =>
                    entity.IsEnabled &&
                    entity.IsHardLimit &&
                    (entity.ScopeType == AiBudgetScopeType.Tenant || entity.UserId == userId)),
                cancellationToken);
            if (hasHardBudget)
            {
                throw new BusinessException(
                    ErrorCode.TooManyRequests,
                    "A priced AI provider is required while a hard budget policy is enabled.");
            }

            await _usageRepository.AddAsync(usage, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var reserve = CalculateCost(
            Math.Max(estimatedInputTokens, 0),
            Math.Max(maxOutputTokens, 0),
            provider.InputTokenPricePerMillion!.Value,
            provider.OutputTokenPricePerMillion!.Value);
        usage.ReservedCost = reserve;
        usage.ReservationExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var currency = provider.PricingCurrency!;
        var lockKey = $"ai:budget:{usage.TenantId:N}:{currency}";
        await _distributedLock.ExecuteWithLockAsync(
            lockKey,
            async token =>
            {
                await EnsureWithinBudgetAsync(usage.TenantId, userId, currency, reserve, token);
                await _usageRepository.AddAsync(usage, token);
                await _unitOfWork.SaveChangesAsync(token);
            },
            expiry: TimeSpan.FromSeconds(15),
            waitTime: TimeSpan.FromSeconds(10),
            cancellationToken: cancellationToken);
    }

    public async Task SettleInvocationAsync(AiUsageLog usage, CancellationToken cancellationToken = default)
    {
        usage.EstimatedCost = usage.PricingCurrency is null ||
            !usage.InputTokenPricePerMillion.HasValue ||
            !usage.OutputTokenPricePerMillion.HasValue ||
            !usage.InputTokens.HasValue ||
            !usage.OutputTokens.HasValue
                ? null
                : CalculateCost(
                    usage.InputTokens.Value,
                    usage.OutputTokens.Value,
                    usage.InputTokenPricePerMillion.Value,
                    usage.OutputTokenPricePerMillion.Value);
        usage.ReservedCost = null;
        usage.ReservationExpiresAt = null;
        _usageRepository.Update(usage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureWithinBudgetAsync(
        Guid tenantId,
        Guid userId,
        string currency,
        decimal requestedReserve,
        CancellationToken cancellationToken)
    {
        var policies = await _queryExecutor.ToListAsync(
            _policyRepository.QueryForTenant(tenantId).Where(entity =>
                entity.IsEnabled &&
                entity.IsHardLimit &&
                entity.Currency == currency &&
                (entity.ScopeType == AiBudgetScopeType.Tenant || entity.UserId == userId)),
            cancellationToken);
        if (policies.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var usageLogs = await _queryExecutor.ToListAsync(
            _usageRepository.QueryForTenant(tenantId).Where(entity =>
                entity.CreatedAt >= monthStart &&
                entity.PricingCurrency == currency &&
                (entity.EstimatedCost.HasValue ||
                    (entity.ReservedCost.HasValue && entity.ReservationExpiresAt > now))),
            cancellationToken);
        var runIds = usageLogs.Select(entity => entity.RunId).Distinct().ToList();
        var runActors = (await _queryExecutor.ToListAsync(
                _runRepository.QueryForTenant(tenantId).Where(entity => runIds.Contains(entity.Id)),
                cancellationToken))
            .ToDictionary(entity => entity.Id, entity => entity.ActorUserId);

        foreach (var policy in policies)
        {
            var current = usageLogs
                .Where(log => policy.ScopeType == AiBudgetScopeType.Tenant ||
                    (runActors.TryGetValue(log.RunId, out var actorId) && actorId == policy.UserId))
                .Sum(log => log.EstimatedCost ?? log.ReservedCost ?? 0m);
            if (current + requestedReserve > policy.MonthlyLimit)
            {
                throw new BusinessException(ErrorCode.TooManyRequests, "The configured AI monthly budget has been exhausted.");
            }
        }
    }

    private static bool HasPricing(AiProviderConfig provider) =>
        provider.InputTokenPricePerMillion.HasValue &&
        provider.OutputTokenPricePerMillion.HasValue &&
        !string.IsNullOrWhiteSpace(provider.PricingCurrency);

    private static decimal CalculateCost(int inputTokens, int outputTokens, decimal inputPrice, decimal outputPrice)
    {
        return decimal.Round(
            inputTokens * inputPrice / 1_000_000m + outputTokens * outputPrice / 1_000_000m,
            6,
            MidpointRounding.AwayFromZero);
    }

    private static string NormalizeRequired(string value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maxLength)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "AI budget policy name is invalid.");
        }

        return normalized;
    }

    private static AiBudgetPolicyResponse ToResponse(AiBudgetPolicy entity, decimal currentAmount)
    {
        return new AiBudgetPolicyResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            PolicyCode = entity.PolicyCode,
            PolicyName = entity.PolicyName,
            ScopeType = entity.ScopeType,
            UserId = entity.UserId,
            MonthlyLimit = entity.MonthlyLimit,
            Currency = entity.Currency,
            IsHardLimit = entity.IsHardLimit,
            AlertThresholdPercentage = entity.AlertThresholdPercentage,
            IsEnabled = entity.IsEnabled,
            CurrentAmount = currentAmount,
            IsAlertThresholdExceeded = currentAmount >= entity.MonthlyLimit * entity.AlertThresholdPercentage / 100m,
            IsLimitExceeded = currentAmount >= entity.MonthlyLimit,
            ConcurrencyToken = entity.RowVersion
        };
    }
}
