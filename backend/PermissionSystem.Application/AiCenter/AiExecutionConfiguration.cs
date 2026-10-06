namespace PermissionSystem.Application.AiCenter;

public sealed record AiExecutedModelConfiguration(int Sequence, int Round, Guid ProviderId, string Model,
    string RouteRole, decimal? Temperature, int MaxTokens, string ModelFingerprint, string SystemPromptHash);

public sealed class AiExecutionConfiguration
{
    public string Stage { get; init; } = "ModelRequestPrepared";
    public string Kind { get; init; } = "BuiltinCompatibility";
    public string BuildIdentity { get; init; } = string.Empty;
    public string? ScenarioContentHash { get; init; }
    public string BasePromptHash { get; init; } = string.Empty;
    public AiScenarioToolSnapshot[] Tools { get; init; } = [];
    public int MaxModelRounds { get; init; } = 6;
    public int MaxToolCalls { get; init; } = 10;
    public int MaxHistoryMessages { get; init; } = 20;
    public int MaxRunSeconds { get; init; } = 90;
    public List<AiExecutedModelConfiguration> Requests { get; init; } = [];
}
