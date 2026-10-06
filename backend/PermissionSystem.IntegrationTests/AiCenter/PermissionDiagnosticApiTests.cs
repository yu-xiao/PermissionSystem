using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Controllers;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class PermissionDiagnosticApiTests
{
    [Theory]
    [InlineData(false, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, HttpStatusCode.Forbidden)]
    [InlineData(true, true, HttpStatusCode.OK)]
    public async Task Endpoint_ShouldUseActualAuthenticationAndPermissionPolicy(bool authenticated, bool allowed, HttpStatusCode expected)
    {
        var service = new ContractService();
        using var server = await CreateServerAsync(service);
        using var client = server.GetTestClient();
        if (authenticated) client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        if (allowed) client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.ToolQueryPermission);
        using var response = await client.PostAsync("/api/ai/permission-diagnostics", Json("{\"kind\":\"DataScope\"}"));
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, service.CallCount);
        if (expected == HttpStatusCode.OK)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("DataScope", body.RootElement.GetProperty("data").GetProperty("target").GetProperty("kind").GetString());
            Assert.Equal("Limited", body.RootElement.GetProperty("data").GetProperty("conclusion").GetString());
        }
    }

    [Theory]
    [InlineData("{\"kind\":\"DataScope\",\"tenantId\":\"10000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"kind\":\"DataScope\",\"actorUserId\":\"30000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"kind\":\"Unknown\"}")]
    [InlineData("{\"kind\":\"Menu\",\"menuId\":\"invalid\"}")]
    public async Task Endpoint_ShouldRejectUnknownIdentityFieldsAndInvalidBindingsBeforeService(string json)
    {
        var service = new ContractService();
        using var server = await CreateServerAsync(service);
        using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.ToolQueryPermission);
        using var response = await client.PostAsync("/api/ai/permission-diagnostics", Json(json));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, service.CallCount);
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    private static Task<IHost> CreateServerAsync(ContractService service)
    {
        return new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.AddHttpContextAccessor();
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            services.AddAuthorization();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IPermissionDiagnosticService>(service);
            services.AddControllers().AddApplicationPart(typeof(AiPermissionDiagnosticController).Assembly);
        }).Configure(app =>
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        })).StartAsync();
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-Authenticated"] != "true") return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity("Test");
            foreach (var code in Request.Headers["X-Test-Permission"])
                identity.AddClaim(new Claim(ClaimConstants.PermissionCode, code!));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }

    private sealed class ContractService : IPermissionDiagnosticService
    {
        public int CallCount { get; private set; }
        public Task<PermissionDiagnosticResponse> DiagnoseAsync(PermissionDiagnosticRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new PermissionDiagnosticResponse
            {
                Target = new() { Kind = request.Kind!.Value }, Conclusion = PermissionDiagnosticConclusion.Limited
            });
        }
        public Task<bool> CanReadAsync(PermissionDiagnosticResponse result, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
