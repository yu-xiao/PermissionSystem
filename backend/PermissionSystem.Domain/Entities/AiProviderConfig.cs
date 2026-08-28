using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Domain.Entities;

public sealed class AiProviderConfig : BaseEntity
{
    public string ProviderCode { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public AiProviderType ProviderType { get; set; } = AiProviderType.OpenAiCompatible;

    public string BaseUrl { get; set; } = string.Empty;

    public string ChatCompletionsPath { get; set; } = "v1/chat/completions";

    public string ApiKeyEncrypted { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 30;

    public decimal? Temperature { get; set; }

    public int? MaxTokens { get; set; }

    public bool AllowInsecureHttp { get; set; }

    public bool AllowPrivateNetwork { get; set; }

    public string AllowedHostsJson { get; set; } = "[]";

    public string? DataResidency { get; set; }

    public DateTimeOffset? ComplianceConfirmedAt { get; set; }

    public string? Remark { get; set; }
}
