using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiModelRoutePolicyResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string AgentCode { get; init; } = string.Empty;

    public Guid PrimaryProviderConfigId { get; init; }

    public Guid? CanaryProviderConfigId { get; init; }

    public int CanaryPercentage { get; init; }

    public Guid? FallbackProviderConfigId { get; init; }

    public bool IsEnabled { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class SaveAiModelRoutePolicyRequest
{
    public Guid? TenantId { get; init; }

    public string AgentCode { get; init; } = string.Empty;

    public Guid PrimaryProviderConfigId { get; init; }

    public Guid? CanaryProviderConfigId { get; init; }

    public int CanaryPercentage { get; init; }

    public Guid? FallbackProviderConfigId { get; init; }

    public bool IsEnabled { get; init; } = true;

    public byte[]? ConcurrencyToken { get; init; }
}

public sealed record AiModelRouteCandidate(AiProviderConfig Provider, AiModelRouteRole Role);

public sealed class AiModelRouteProviderOptionResponse
{
    public Guid Id { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public string ModelName { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public bool IsComplianceConfirmed { get; init; }

    public bool SupportsTools { get; init; }

    public string? DataResidency { get; init; }

    public string? PricingCurrency { get; init; }
}

public interface IAiModelRouteService
{
    Task<IReadOnlyList<AiModelRoutePolicyResponse>> GetPoliciesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiModelRouteProviderOptionResponse>> GetProviderOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<AiModelRoutePolicyResponse> SavePolicyAsync(
        SaveAiModelRoutePolicyRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiModelRouteCandidate>> ResolveAsync(
        string agentCode,
        Guid conversationId,
        CancellationToken cancellationToken = default);
}
