using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PermissionSystem.McpServer.Configuration;
using PermissionSystem.McpServer.Middlewares;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class McpProtectedResourceMetadataTests
{
    [Fact]
    public void Metadata_UsesConfiguredResourceUrl()
    {
        var options = CreateOptions();

        var metadata = new McpProtectedResourceMetadata(options);

        Assert.Equal("https://mcp.example.test/mcp", metadata.Document.Resource);
        Assert.Equal(
            "https://mcp.example.test/.well-known/oauth-protected-resource/mcp",
            metadata.ResourceMetadataUrl);
        Assert.Equal(["https://identity.example.test/"], metadata.Document.AuthorizationServers);
        Assert.Equal([AiCenterConstants.McpScope], metadata.Document.ScopesSupported);
        Assert.Equal(["header"], metadata.Document.BearerMethodsSupported);
    }

    [Fact]
    public void Challenge_AppendsConfiguredMetadataUrlOnce()
    {
        IHeaderDictionary headers = new HeaderDictionary();
        headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
        const string metadataUrl =
            "https://mcp.example.test/.well-known/oauth-protected-resource/mcp";

        McpResourceMetadataChallengeMiddleware.AppendChallenge(headers, metadataUrl);
        McpResourceMetadataChallengeMiddleware.AppendChallenge(headers, metadataUrl);

        var challenge = Assert.Single(headers.WWWAuthenticate);
        Assert.Equal(
            $"Bearer error=\"invalid_token\", resource_metadata=\"{metadataUrl}\"",
            challenge);
    }

    [Fact]
    public void StartupValidator_RejectsRequestRelativeResourceUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpAuthentication:Authority"] = "https://identity.example.test/",
                ["McpAuthentication:ResourceUrl"] = "/mcp",
                ["McpAuthentication:IntrospectionClientId"] = AiCenterConstants.McpIntrospectionClientId,
                ["McpAuthentication:IntrospectionClientSecret"] = new string('S', 32)
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            McpStartupValidator.Validate(configuration, new TestHostEnvironment("Development")));

        Assert.Contains("ResourceUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupValidator_RejectsInsecureProductionResourceUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpAuthentication:Authority"] = "https://identity.example.test/",
                ["McpAuthentication:ResourceUrl"] = "http://mcp.example.test/mcp",
                ["McpAuthentication:IntrospectionClientId"] = AiCenterConstants.McpIntrospectionClientId,
                ["McpAuthentication:IntrospectionClientSecret"] = new string('S', 32)
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            McpStartupValidator.Validate(configuration, new TestHostEnvironment("Production")));

        Assert.Contains("HTTPS", exception.Message, StringComparison.Ordinal);
    }

    private static McpAuthenticationOptions CreateOptions() => new()
    {
        Authority = "https://identity.example.test/",
        ResourceUrl = "https://mcp.example.test/mcp",
        IntrospectionClientId = AiCenterConstants.McpIntrospectionClientId,
        IntrospectionClientSecret = new string('S', 32)
    };

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "PermissionSystem.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
