namespace PermissionSystem.Infrastructure.Options;

public sealed class OpenAiCompatibleOptions
{
    public const string SectionName = "Ai:OpenAiCompatible";

    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = string.Empty;

    public string ChatCompletionsPath { get; init; } = "v1/chat/completions";

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;

    public bool AllowInsecureHttp { get; init; }

    public bool AllowPrivateNetwork { get; init; }

    public string[] AllowedHosts { get; init; } = [];
}
