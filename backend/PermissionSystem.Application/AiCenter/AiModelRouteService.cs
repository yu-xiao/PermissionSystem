using System.Buffers.Binary;
using System.Security.Cryptography;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiModelRouteService : IAiModelRouteService
{
    private readonly IRepository<AiModelRoutePolicy> _policyRepository;
    private readonly IRepository<AiProviderConfig> _providerRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IUnitOfWork _unitOfWork;

    public AiModelRouteService(
        IRepository<AiModelRoutePolicy> policyRepository,
        IRepository<AiProviderConfig> providerRepository,
        IAsyncQueryExecutor queryExecutor,
        ITenantWriteResolver tenantWriteResolver,
        IUnitOfWork unitOfWork)
    {
        _policyRepository = policyRepository;
        _providerRepository = providerRepository;
        _queryExecutor = queryExecutor;
        _tenantWriteResolver = tenantWriteResolver;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<AiModelRoutePolicyResponse>> GetPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        var policies = await _queryExecutor.ToListAsync(
            _policyRepository.Query().OrderBy(entity => entity.AgentCode),
            cancellationToken);
        return policies.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<AiModelRouteProviderOptionResponse>> GetProviderOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var providers = await _queryExecutor.ToListAsync(
            _providerRepository.Query().OrderBy(entity => entity.ProviderName),
            cancellationToken);
        return providers.Select(entity => new AiModelRouteProviderOptionResponse
        {
            Id = entity.Id,
            ProviderName = entity.ProviderName,
            ModelName = entity.ModelName,
            IsEnabled = entity.IsEnabled,
            IsComplianceConfirmed = entity.ComplianceConfirmedAt.HasValue,
            SupportsTools = entity.SupportsTools,
            DataResidency = entity.DataResidency,
            PricingCurrency = entity.PricingCurrency
        }).ToList();
    }

    public async Task<AiModelRoutePolicyResponse> SavePolicyAsync(
        SaveAiModelRoutePolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        var agentCode = NormalizeAgentCode(request.AgentCode);
        var providerIds = new[]
            {
                request.PrimaryProviderConfigId,
                request.CanaryProviderConfigId,
                request.FallbackProviderConfigId
            }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (request.PrimaryProviderConfigId == Guid.Empty || providerIds.Distinct().Count() != providerIds.Count)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Model route providers must be non-empty and distinct.");
        }

        if (request.CanaryProviderConfigId.HasValue && request.CanaryPercentage is < 1 or > 100)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Canary percentage must be between 1 and 100.");
        }

        if (!request.CanaryProviderConfigId.HasValue && request.CanaryPercentage != 0)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Canary percentage must be zero when no canary provider is configured.");
        }

        var providers = await _queryExecutor.ToListAsync(
            _providerRepository.QueryForTenant(tenantId).Where(entity => providerIds.Contains(entity.Id)),
            cancellationToken);
        if (providers.Count != providerIds.Count)
        {
            throw new BusinessException(ErrorCode.NotFound, "One or more AI providers were not found in the selected tenant.");
        }

        ValidateProviders(providers, request.PrimaryProviderConfigId);

        var policy = await _queryExecutor.FirstOrDefaultAsync(
            _policyRepository.QueryForTenant(tenantId).Where(entity => entity.AgentCode == agentCode),
            cancellationToken);
        if (policy is null)
        {
            policy = new AiModelRoutePolicy
            {
                TenantId = tenantId,
                AgentCode = agentCode
            };
            await _policyRepository.AddAsync(policy, cancellationToken);
        }
        else
        {
            ConcurrencyTokenGuard.EnsureMatches(policy, request.ConcurrencyToken);
            _policyRepository.Update(policy);
        }

        policy.PrimaryProviderConfigId = request.PrimaryProviderConfigId;
        policy.CanaryProviderConfigId = request.CanaryProviderConfigId;
        policy.CanaryPercentage = request.CanaryPercentage;
        policy.FallbackProviderConfigId = request.FallbackProviderConfigId;
        policy.IsEnabled = request.IsEnabled;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(policy);
    }

    public async Task<IReadOnlyList<AiModelRouteCandidate>> ResolveAsync(
        string agentCode,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedAgentCode = NormalizeAgentCode(agentCode);
        var policy = await _queryExecutor.FirstOrDefaultAsync(
            _policyRepository.Query().Where(entity => entity.AgentCode == normalizedAgentCode && entity.IsEnabled),
            cancellationToken);
        if (policy is null)
        {
            var defaultProvider = await _queryExecutor.FirstOrDefaultAsync(
                _providerRepository.Query().Where(entity => entity.IsDefault && entity.IsEnabled),
                cancellationToken)
                ?? throw new BusinessException(ErrorCode.Conflict, "No enabled default AI provider is configured.");
            EnsureRuntimeEligible(defaultProvider);
            return [new AiModelRouteCandidate(defaultProvider, AiModelRouteRole.Primary)];
        }

        var providerIds = new[]
            {
                policy.PrimaryProviderConfigId,
                policy.CanaryProviderConfigId,
                policy.FallbackProviderConfigId
            }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        var providers = await _queryExecutor.ToListAsync(
            _providerRepository.Query().Where(entity => providerIds.Contains(entity.Id)),
            cancellationToken);
        var byId = providers.ToDictionary(entity => entity.Id);
        var useCanary = policy.CanaryProviderConfigId.HasValue &&
            StableBucket(conversationId) < policy.CanaryPercentage;
        var selectedId = useCanary ? policy.CanaryProviderConfigId!.Value : policy.PrimaryProviderConfigId;
        var candidates = new List<AiModelRouteCandidate>();
        if (byId.TryGetValue(selectedId, out var selected) && IsRuntimeEligible(selected))
        {
            candidates.Add(new AiModelRouteCandidate(
                selected,
                useCanary ? AiModelRouteRole.Canary : AiModelRouteRole.Primary));
        }

        if (policy.FallbackProviderConfigId.HasValue &&
            byId.TryGetValue(policy.FallbackProviderConfigId.Value, out var fallback) &&
            IsRuntimeEligible(fallback) &&
            fallback.Id != selectedId)
        {
            candidates.Add(new AiModelRouteCandidate(fallback, AiModelRouteRole.Fallback));
        }

        if (candidates.Count == 0)
        {
            throw new BusinessException(ErrorCode.Conflict, "The active AI model route has no eligible provider.");
        }

        return candidates;
    }

    private static void ValidateProviders(IReadOnlyCollection<AiProviderConfig> providers, Guid primaryProviderId)
    {
        foreach (var provider in providers)
        {
            EnsureRuntimeEligible(provider);
        }

        var primary = providers.Single(entity => entity.Id == primaryProviderId);
        if (providers.Any(entity => !string.Equals(
                entity.DataResidency?.Trim(),
                primary.DataResidency?.Trim(),
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "All providers in a route must have the same data residency classification.");
        }

        if (providers.Any(entity => !string.Equals(
                entity.PricingCurrency?.Trim(),
                primary.PricingCurrency?.Trim(),
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "All priced providers in a route must use the same currency; currency conversion is not supported.");
        }
    }

    private static void EnsureRuntimeEligible(AiProviderConfig provider)
    {
        if (!IsRuntimeEligible(provider))
        {
            throw new BusinessException(
                ErrorCode.Conflict,
                "AI route providers must be enabled, compliance-confirmed, and support tool calling.");
        }
    }

    private static bool IsRuntimeEligible(AiProviderConfig provider) =>
        provider.IsEnabled && provider.ComplianceConfirmedAt.HasValue && provider.SupportsTools;

    private static int StableBucket(Guid conversationId)
    {
        var digest = SHA256.HashData(conversationId.ToByteArray());
        return (int)(BinaryPrimitives.ReadUInt32BigEndian(digest) % 100);
    }

    private static string NormalizeAgentCode(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > 100 ||
            normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Agent code is invalid.");
        }

        return normalized;
    }

    private static AiModelRoutePolicyResponse ToResponse(AiModelRoutePolicy entity)
    {
        return new AiModelRoutePolicyResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AgentCode = entity.AgentCode,
            PrimaryProviderConfigId = entity.PrimaryProviderConfigId,
            CanaryProviderConfigId = entity.CanaryProviderConfigId,
            CanaryPercentage = entity.CanaryPercentage,
            FallbackProviderConfigId = entity.FallbackProviderConfigId,
            IsEnabled = entity.IsEnabled,
            ConcurrencyToken = entity.RowVersion
        };
    }
}
