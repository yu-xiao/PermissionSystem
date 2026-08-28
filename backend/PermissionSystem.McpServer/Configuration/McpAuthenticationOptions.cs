namespace PermissionSystem.McpServer.Configuration;

public sealed class McpAuthenticationOptions
{
    public const string SectionName = "McpAuthentication";

    public string Authority { get; init; } = string.Empty;

    public string IntrospectionClientId { get; init; } = string.Empty;

    public string IntrospectionClientSecret { get; init; } = string.Empty;
}
