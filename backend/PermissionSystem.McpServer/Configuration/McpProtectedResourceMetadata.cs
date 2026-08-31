using System.Text.Json.Serialization;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.McpServer.Configuration;

internal sealed class McpProtectedResourceMetadata
{
    public McpProtectedResourceMetadata(McpAuthenticationOptions options)
    {
        var resourceUri = new Uri(options.ResourceUrl);
        var resourcePath = resourceUri.AbsolutePath.TrimEnd('/');
        ResourceMetadataUrl = new UriBuilder(
            resourceUri.Scheme,
            resourceUri.Host,
            resourceUri.Port,
            $"/.well-known/oauth-protected-resource{resourcePath}").Uri.AbsoluteUri;
        Document = new McpProtectedResourceMetadataDocument
        {
            Resource = resourceUri.AbsoluteUri,
            AuthorizationServers = [new Uri(options.Authority).AbsoluteUri],
            ScopesSupported = [AiCenterConstants.McpScope],
            BearerMethodsSupported = ["header"],
            ResourceName = "PermissionSystem MCP Server"
        };
    }

    public string ResourceMetadataUrl { get; }

    public McpProtectedResourceMetadataDocument Document { get; }
}

internal sealed class McpProtectedResourceMetadataDocument
{
    [JsonPropertyName("resource")]
    public string Resource { get; init; } = string.Empty;

    [JsonPropertyName("authorization_servers")]
    public IReadOnlyList<string> AuthorizationServers { get; init; } = [];

    [JsonPropertyName("scopes_supported")]
    public IReadOnlyList<string> ScopesSupported { get; init; } = [];

    [JsonPropertyName("bearer_methods_supported")]
    public IReadOnlyList<string> BearerMethodsSupported { get; init; } = [];

    [JsonPropertyName("resource_name")]
    public string ResourceName { get; init; } = string.Empty;
}
