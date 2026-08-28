using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.AiCenter;

public sealed class AiProviderConnectionSettings
{
    public AiProviderType ProviderType { get; init; } = AiProviderType.OpenAiCompatible;

    public string BaseUrl { get; init; } = string.Empty;

    public string ChatCompletionsPath { get; init; } = "v1/chat/completions";

    public string ApiKey { get; init; } = string.Empty;

    public string ModelName { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;

    public bool AllowInsecureHttp { get; init; }

    public bool AllowPrivateNetwork { get; init; }

    public IReadOnlyCollection<string> AllowedHosts { get; init; } = [];
}

public interface IAiProviderConnectionTester
{
    void Validate(AiProviderConnectionSettings settings);

    Task<AiProviderConnectionTestResult> TestAsync(
        AiProviderConnectionSettings settings,
        CancellationToken cancellationToken = default);
}

public sealed class AiProviderConnectionTestResult
{
    public bool Succeeded { get; init; }

    public string Message { get; init; } = string.Empty;

    public string ModelName { get; init; } = string.Empty;
}
