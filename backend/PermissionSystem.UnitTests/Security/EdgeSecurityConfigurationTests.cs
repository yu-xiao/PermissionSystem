using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PermissionSystem.Api.Configuration;
using PermissionSystem.Api.Options;
using PermissionSystem.Api.Services;

namespace PermissionSystem.UnitTests.Security;

public sealed class EdgeSecurityConfigurationTests
{
    [Fact]
    public void ClientIpAccessor_ShouldIgnoreForwardedHeaderAndNormalizeMappedIpv4()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.20";

        var clientIp = new ClientIpAccessor().GetClientIp(context);

        Assert.Equal("192.0.2.10", clientIp);
    }

    [Fact]
    public void ProductionConfiguration_ShouldRejectMissingAllowedHosts()
    {
        var configuration = BuildConfiguration(("Cors:AllowedOrigins:0", "https://admin.example.com"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            StartupSecurityValidator.ValidateProductionConfiguration(
                configuration,
                new TestHostEnvironment(Environments.Production)));

        Assert.Contains("AllowedHosts", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionConfiguration_ShouldRejectWildcardAllowedHosts()
    {
        var configuration = BuildConfiguration(
            ("AllowedHosts", "*"),
            ("Cors:AllowedOrigins:0", "https://admin.example.com"));

        Assert.Throws<InvalidOperationException>(() =>
            StartupSecurityValidator.ValidateProductionConfiguration(
                configuration,
                new TestHostEnvironment(Environments.Production)));
    }

    [Fact]
    public void ProductionConfiguration_ShouldRejectWildcardCorsOrigin()
    {
        var configuration = BuildConfiguration(
            ("AllowedHosts", "api.example.com"),
            ("Cors:AllowedOrigins:0", "https://*.example.com"));

        Assert.Throws<InvalidOperationException>(() =>
            StartupSecurityValidator.ValidateProductionConfiguration(
                configuration,
                new TestHostEnvironment(Environments.Production)));
    }

    [Fact]
    public void ProductionConfiguration_ShouldAcceptExplicitHostsAndOrigins()
    {
        var configuration = BuildConfiguration(
            ("AllowedHosts", "api.example.com;login.example.com"),
            ("Cors:AllowedOrigins:0", "https://admin.example.com"));

        StartupSecurityValidator.ValidateProductionConfiguration(
            configuration,
            new TestHostEnvironment(Environments.Production));
    }

    [Fact]
    public void ReverseProxyConfiguration_ShouldRequireTrustedBoundaryWhenEnabled()
    {
        var settings = new ReverseProxyOptions
        {
            Enabled = true,
            ForwardLimit = 1
        };

        Assert.Throws<InvalidOperationException>(() => ReverseProxyConfiguration.Validate(settings));
    }

    [Fact]
    public void ReverseProxyConfiguration_ShouldRejectGlobalTrustedNetwork()
    {
        var settings = new ReverseProxyOptions
        {
            Enabled = true,
            ForwardLimit = 1,
            KnownNetworks = ["0.0.0.0/0"]
        };

        Assert.Throws<InvalidOperationException>(() => ReverseProxyConfiguration.Validate(settings));
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => (string?)value.Value))
            .Build();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "PermissionSystem.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
