using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiCenter;

public sealed record AiScenarioToolSnapshot(string ToolCode, string FunctionName, string Version,
    string Description, string InputSchemaJson, string ModelSchemaJson, string OutputSchemaJson,
    string[] RequiredPermissions, string DataClassification, string DataScopePolicy, int TimeoutSeconds, int? MaxRows);

public sealed record AiScenarioSnapshot
{
    public int ContractVersion { get; init; } = 1;
    public Guid TenantId { get; init; }
    public string ScenarioCode { get; init; } = AiScenarioCatalog.PermissionAssistant;
    public int DraftRevision { get; init; }
    public string BuildIdentity { get; init; } = string.Empty;
    public string SafetyVersion { get; init; } = AiScenarioCatalog.SafetyVersion;
    public string SystemPrompt { get; init; } = string.Empty;
    public int StructuredResultVersion { get; init; } = 1;
    public AiScenarioConfiguration Configuration { get; init; } = new();
    public AiScenarioToolSnapshot[] Tools { get; init; } = [];
}

public static class AiScenarioSnapshots
{
    public const int MaxConfigurationBytes = 256 * 1024;
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 48,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Json<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > MaxConfigurationBytes) Invalid("AI configuration exceeds its storage limit.");
        return json;
    }
    public static T Read<T>(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxConfigurationBytes) Invalid("AI configuration exceeds its storage limit.");
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw new BusinessException(ErrorCode.ValidationFailed, "Invalid AI configuration document."); }
    }

    public static AiScenarioConfiguration Normalize(AiScenarioConfiguration configuration)
    {
        if (configuration is null || configuration.SupplementPrompt is null || configuration.SupplementPrompt.Length > 8000 ||
            configuration.ToolCodes is null || configuration.ToolCodes.Length is < 1 or > 32 ||
            configuration.ToolCodes.Distinct(StringComparer.Ordinal).Count() != configuration.ToolCodes.Length ||
            configuration.ToolCodes.Any(c => !AiScenarioCatalog.PermissionTools.Contains(c, StringComparer.Ordinal)) ||
            configuration.MaxModelRounds is < 1 or > 6 || configuration.MaxToolCalls is < 1 or > 10 ||
            configuration.MaxHistoryMessages is < 1 or > 20 || configuration.MaxRunSeconds is < 1 or > 90 ||
            configuration.Temperature is < 0 or > 2 || configuration.MaxTokens is < 1 or > 128000)
            Invalid("Invalid permission assistant configuration.");
        return configuration! with { SupplementPrompt = configuration.SupplementPrompt.Trim(),
            ToolCodes = configuration.ToolCodes.Order(StringComparer.Ordinal).ToArray() };
    }

    public static AiScenarioToolSnapshot Tool(AiToolDefinition definition) => new(definition.ToolCode,
        definition.FunctionName, definition.Version, definition.Description,
        CanonicalSchema(definition.InputSchemaJson), CanonicalSchema(AiFollowUpContextService.ExtendModelSchema(definition)),
        CanonicalSchema(definition.OutputSchemaJson), definition.RequiredPermissions.Order(StringComparer.Ordinal).ToArray(),
        definition.DataClassification, definition.DataScopePolicy, definition.TimeoutSeconds, definition.MaxRows);

    public static string CanonicalSchema(string json)
    {
        JsonNode? Sort(JsonNode? node) => node switch
        {
            JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new KeyValuePair<string, JsonNode?>(p.Key, Sort(p.Value)))),
            JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
            _ => node?.DeepClone()
        };
        return Sort(JsonNode.Parse(json))!.ToJsonString();
    }

    public static string Prompt(AiScenarioConfiguration configuration) => AiScenarioCatalog.SafetyPrompt +
        (string.IsNullOrEmpty(configuration.SupplementPrompt) ? "" : "\nScenario instructions:\n" + configuration.SupplementPrompt);

    public static void Validate(AiScenarioSnapshot snapshot, string buildIdentity, IEnumerable<AiToolDefinition> catalog)
    {
        var configuration = Normalize(snapshot.Configuration);
        if (string.IsNullOrWhiteSpace(buildIdentity) || snapshot.ContractVersion != 1 || snapshot.ScenarioCode != AiScenarioCatalog.PermissionAssistant ||
            snapshot.BuildIdentity != buildIdentity || snapshot.SafetyVersion != AiScenarioCatalog.SafetyVersion ||
            snapshot.StructuredResultVersion != 1 || snapshot.SystemPrompt != Prompt(configuration) ||
            Json(configuration) != Json(snapshot.Configuration) || snapshot.Tools is null)
            Invalid("The AI version is incompatible with the current build or safety policy.");
        var tools = catalog.Where(d => configuration.ToolCodes.Contains(d.ToolCode, StringComparer.Ordinal))
            .OrderBy(d => d.ToolCode, StringComparer.Ordinal).Select(Tool).ToArray();
        if (tools.Length != configuration.ToolCodes.Length || tools.Length > 32 || Json(tools) != Json(snapshot.Tools))
            Invalid("The AI version tool contract is incompatible with the current build.");
    }

    public static string ModelFingerprint(AiProviderConfig provider, AiScenarioConfiguration configuration) =>
        ModelFingerprint(new AiProviderConnectionSettings
        {
            ProviderType = provider.ProviderType, BaseUrl = provider.BaseUrl, ChatCompletionsPath = provider.ChatCompletionsPath,
            ModelName = provider.ModelName, TimeoutSeconds = provider.TimeoutSeconds, AllowInsecureHttp = provider.AllowInsecureHttp,
            AllowPrivateNetwork = provider.AllowPrivateNetwork,
            AllowedHosts = JsonSerializer.Deserialize<string[]>(provider.AllowedHostsJson) ?? []
        }, configuration.Temperature, configuration.MaxTokens);

    public static string ModelFingerprint(AiProviderConnectionSettings provider, decimal? temperature, int maxTokens) => Digest(Json(new
    {
        provider.ProviderType, baseUrl = provider.BaseUrl.Trim().TrimEnd('/'), path = provider.ChatCompletionsPath.Trim().TrimStart('/'),
        provider.ModelName, provider.TimeoutSeconds, provider.AllowInsecureHttp, provider.AllowPrivateNetwork,
        allowedHosts = provider.AllowedHosts.Select(h => h.Trim().ToLowerInvariant()).Order(StringComparer.Ordinal).ToArray(), temperature, maxTokens
    }));

    [DoesNotReturn]
    private static void Invalid(string message) => throw new BusinessException(ErrorCode.ValidationFailed, message);
}

public sealed class AiScenarioSnapshotFactory(IEnumerable<IAiReadOnlyToolHandler> handlers, IAiBuildIdentity build)
{
    private readonly AiToolDefinition[] _catalog = handlers.Select(h => h.Definition).ToArray();
    public AiScenarioSnapshot Create(Guid tenantId, int revision, AiScenarioConfiguration configuration)
    {
        configuration = AiScenarioSnapshots.Normalize(configuration);
        var snapshot = new AiScenarioSnapshot
        {
            TenantId = tenantId, DraftRevision = revision, BuildIdentity = build.Identity,
            Configuration = configuration, SystemPrompt = AiScenarioSnapshots.Prompt(configuration),
            Tools = _catalog.Where(t => configuration.ToolCodes.Contains(t.ToolCode, StringComparer.Ordinal))
                .OrderBy(t => t.ToolCode, StringComparer.Ordinal).Select(AiScenarioSnapshots.Tool).ToArray()
        };
        Validate(snapshot);
        return snapshot;
    }
    public void Validate(AiScenarioSnapshot snapshot) => AiScenarioSnapshots.Validate(snapshot, build.Identity, _catalog);
}
