using PermissionSystem.Shared.Constants;

namespace PermissionSystem.McpServer.Configuration;

internal static class McpStartupValidator
{
    public static McpAuthenticationOptions Validate(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = configuration
            .GetSection(McpAuthenticationOptions.SectionName)
            .Get<McpAuthenticationOptions>() ?? new McpAuthenticationOptions();

        if (!Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority) ||
            (!string.Equals(authority.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(authority.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrEmpty(authority.UserInfo))
        {
            throw new InvalidOperationException("McpAuthentication:Authority must be an absolute HTTP or HTTPS URL.");
        }

        if (!environment.IsDevelopment() &&
            !environment.IsEnvironment("Docker") &&
            authority.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("MCP authentication authority must use HTTPS outside development environments.");
        }

        if (!Uri.TryCreate(options.ResourceUrl, UriKind.Absolute, out var resourceUrl) ||
            (!string.Equals(resourceUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(resourceUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrEmpty(resourceUrl.UserInfo) ||
            !string.IsNullOrEmpty(resourceUrl.Query) ||
            !string.IsNullOrEmpty(resourceUrl.Fragment) ||
            !resourceUrl.AbsolutePath.TrimEnd('/').EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "McpAuthentication:ResourceUrl must be an absolute HTTP or HTTPS MCP URL without query or fragment.");
        }

        if (!environment.IsDevelopment() &&
            !environment.IsEnvironment("Docker") &&
            resourceUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("MCP resource URL must use HTTPS outside development environments.");
        }

        if (!string.Equals(
                options.IntrospectionClientId,
                AiCenterConstants.McpIntrospectionClientId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The MCP introspection client identifier is invalid.");
        }

        if (string.IsNullOrWhiteSpace(options.IntrospectionClientSecret) ||
            options.IntrospectionClientSecret.Length < 32)
        {
            throw new InvalidOperationException(
                "McpAuthentication:IntrospectionClientSecret must contain at least 32 characters.");
        }

        return options;
    }
}
