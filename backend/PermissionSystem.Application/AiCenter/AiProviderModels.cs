using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Pagination;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiProviderQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public bool? Enabled { get; init; }
}

public class AiProviderListResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string ProviderCode { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public AiProviderType ProviderType { get; init; }

    public string BaseUrl { get; init; } = string.Empty;

    public string ModelName { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public bool IsEnabled { get; init; }

    public string? DataResidency { get; init; }

    public bool SupportsTools { get; init; }

    public bool SupportsJsonSchema { get; init; }

    public decimal? InputTokenPricePerMillion { get; init; }

    public decimal? OutputTokenPricePerMillion { get; init; }

    public string? PricingCurrency { get; init; }

    public DateTimeOffset? ComplianceConfirmedAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class AiProviderDetailResponse : AiProviderListResponse
{
    public string ChatCompletionsPath { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public bool HasApiKey { get; init; }

    public int TimeoutSeconds { get; init; }

    public decimal? Temperature { get; init; }

    public int? MaxTokens { get; init; }

    public bool AllowInsecureHttp { get; init; }

    public bool AllowPrivateNetwork { get; init; }

    public IReadOnlyList<string> AllowedHosts { get; init; } = [];

    public string? Remark { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class CreateAiProviderRequest
{
    public Guid? TenantId { get; init; }

    public string ProviderCode { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public AiProviderType ProviderType { get; init; } = AiProviderType.OpenAiCompatible;

    public string BaseUrl { get; init; } = string.Empty;

    public string ChatCompletionsPath { get; init; } = "v1/chat/completions";

    public string ApiKey { get; init; } = string.Empty;

    public string ModelName { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public bool IsEnabled { get; init; } = true;

    public int TimeoutSeconds { get; init; } = 30;

    public decimal? Temperature { get; init; }

    public int? MaxTokens { get; init; }

    public bool AllowInsecureHttp { get; init; }

    public bool AllowPrivateNetwork { get; init; }

    public IReadOnlyCollection<string> AllowedHosts { get; init; } = [];

    public string? DataResidency { get; init; }

    public bool SupportsTools { get; init; } = true;

    public bool SupportsJsonSchema { get; init; }

    public decimal? InputTokenPricePerMillion { get; init; }

    public decimal? OutputTokenPricePerMillion { get; init; }

    public string? PricingCurrency { get; init; }

    public string? Remark { get; init; }
}

public sealed class UpdateAiProviderRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public string ChatCompletionsPath { get; init; } = "v1/chat/completions";

    public string? ApiKey { get; init; }

    public string ModelName { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;

    public decimal? Temperature { get; init; }

    public int? MaxTokens { get; init; }

    public bool AllowInsecureHttp { get; init; }

    public bool AllowPrivateNetwork { get; init; }

    public IReadOnlyCollection<string> AllowedHosts { get; init; } = [];

    public string? DataResidency { get; init; }

    public bool SupportsTools { get; init; } = true;

    public bool SupportsJsonSchema { get; init; }

    public decimal? InputTokenPricePerMillion { get; init; }

    public decimal? OutputTokenPricePerMillion { get; init; }

    public string? PricingCurrency { get; init; }

    public string? Remark { get; init; }
}

public sealed class SetAiProviderEnabledRequest
{
    public bool IsEnabled { get; init; }

    public byte[]? ConcurrencyToken { get; init; }
}

public sealed class SetAiProviderComplianceRequest
{
    public bool IsConfirmed { get; init; }

    public byte[]? ConcurrencyToken { get; init; }
}
