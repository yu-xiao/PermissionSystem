using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PermissionSystem.Api.Middlewares;

namespace PermissionSystem.IntegrationTests.Security;

public sealed class ForwardedHeadersSecurityTests
{
    [Fact]
    public async Task UntrustedClient_ShouldNotOverrideRemoteIpOrSchemeWithForwardedHeaders()
    {
        using var host = await CreateForwardedHeadersServerAsync();
        using var client = host.GetTestClient();
        using var request = CreateRequest("203.0.113.10", "198.51.100.20", "https");

        var result = await client.SendAsync(request);
        var value = await result.Content.ReadAsStringAsync();

        Assert.Equal("203.0.113.10|http|localhost", value);
    }

    [Fact]
    public async Task TrustedProxy_ShouldSetRemoteIpSchemeAndHostFromForwardedHeaders()
    {
        using var host = await CreateForwardedHeadersServerAsync();
        using var client = host.GetTestClient();
        using var request = CreateRequest("10.0.0.10", "198.51.100.20", "https");

        var result = await client.SendAsync(request);
        var value = await result.Content.ReadAsStringAsync();

        Assert.Equal("198.51.100.20|https|api.example.com", value);
    }

    [Fact]
    public async Task SecurityHeadersMiddleware_ShouldAddBaselineHeaders()
    {
        using var host = await CreateSecurityHeadersServerAsync();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("camera=()", response.Headers.GetValues("Permissions-Policy").Single());
    }

    private static Task<IHost> CreateForwardedHeadersServerAsync()
    {
        return new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseTestServer();
                builder.ConfigureServices(services =>
                {
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                            ForwardedHeaders.XForwardedProto |
                            ForwardedHeaders.XForwardedHost;
                        options.ForwardLimit = 1;
                        options.RequireHeaderSymmetry = true;
                        options.KnownProxies.Clear();
                        options.KnownIPNetworks.Clear();
                        options.KnownProxies.Add(IPAddress.Parse("10.0.0.10"));
                        options.AllowedHosts.Add("api.example.com");
                    });
                });
                builder.Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["X-Test-Remote-Ip"]!);
                        await next(context);
                    });
                    app.UseForwardedHeaders();
                    app.Run(context => context.Response.WriteAsync(
                        $"{context.Connection.RemoteIpAddress}|{context.Request.Scheme}|{context.Request.Host.Host}"));
                });
            })
            .StartAsync();
    }

    private static Task<IHost> CreateSecurityHeadersServerAsync()
    {
        return new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseTestServer();
                builder.Configure(app =>
                {
                    app.UseMiddleware<SecurityHeadersMiddleware>();
                    app.Run(context => context.Response.WriteAsync("ok"));
                });
            })
            .StartAsync();
    }

    private static HttpRequestMessage CreateRequest(string remoteIp, string forwardedFor, string forwardedProto)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Test-Remote-Ip", remoteIp);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        request.Headers.Add("X-Forwarded-Host", "api.example.com");
        return request;
    }
}
