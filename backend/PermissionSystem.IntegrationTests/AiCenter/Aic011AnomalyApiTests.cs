using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Controllers;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiAnomalies;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic011AnomalyApiTests
{
    [Theory]
    [InlineData("rules")]
    [InlineData("events/11111111-1111-1111-1111-111111111111")]
    [InlineData("rules/11111111-1111-1111-1111-111111111111/events")]
    public async Task ReadRoutes_RequireOriginalPolicyAndDisableCaching(string path)
    {
        var state = new ContractService(); using var host = await Server(state); using var client = host.GetTestClient();
        using var anonymous = await client.GetAsync($"/api/ai/anomalies/{path}"); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        using var denied = await client.GetAsync($"/api/ai/anomalies/{path}"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Permission", AiAnomalyContract.ViewPermission);
        using var allowed = await client.GetAsync($"/api/ai/anomalies/{path}"); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode); Assert.True(allowed.Headers.CacheControl?.NoStore);
        Assert.Equal(1, state.Reads);
    }
    [Theory]
    [InlineData("rules", AiAnomalyContract.CreatePermission)]
    [InlineData("rules/11111111-1111-1111-1111-111111111111/check", AiAnomalyContract.TriggerPermission)]
    public async Task CreateAndTrigger_UseSeparatePermissions(string path, string permission)
    {
        var state = new ContractService(); using var host = await Server(state); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", AiAnomalyContract.ViewPermission);
        using var denied = await client.PostAsync($"/api/ai/anomalies/{path}", null); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", permission);
        using var allowed = await client.PostAsync($"/api/ai/anomalies/{path}", null); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode); Assert.Equal(1, state.Writes);
    }
    private static Task<IHost> Server(ContractService state) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
        .ConfigureServices(services =>
        {
            services.AddHttpContextAccessor(); services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
            services.AddAuthorization(); services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>(); services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IAiAnomalyService>(state); services.AddControllers().AddApplicationPart(typeof(AiAnomalyController).Assembly);
        }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).StartAsync();
    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-Authenticated"] != "true") return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity("Test"); identity.AddClaim(new(ClaimConstants.UserId, "11111111-1111-1111-1111-111111111111"));
            foreach (var code in Request.Headers["X-Test-Permission"]) identity.AddClaim(new(ClaimConstants.PermissionCode, code!));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), "Test")));
        }
    }
    private sealed class ContractService : IAiAnomalyService
    {
        public int Reads { get; private set; } public int Writes { get; private set; }
        public Task<PagedResult<AiAnomalyRuleResponse>> ListAsync(int pageIndex, int pageSize, CancellationToken ct = default)
        { Reads++; return Task.FromResult(PagedResult<AiAnomalyRuleResponse>.Create([], pageIndex, pageSize, 0)); }
        public Task<PagedResult<AiAnomalyEventResponse>> EventsAsync(Guid ruleId, int pageIndex, int pageSize, CancellationToken ct = default)
        { Reads++; return Task.FromResult(PagedResult<AiAnomalyEventResponse>.Create([], pageIndex, pageSize, 0)); }
        public Task<AiAnomalyEventResponse> EventAsync(Guid id, CancellationToken ct = default)
        { Reads++; return Task.FromResult(new AiAnomalyEventResponse(id, Guid.NewGuid(), 1, null, DateTimeOffset.UtcNow, null, null, "Pending", 0, null, null, [], true, null)); }
        public Task<AiAnomalyRuleResponse> CreateAsync(CancellationToken ct = default)
        { Writes++; return Task.FromResult(new AiAnomalyRuleResponse(Guid.NewGuid(), false, null, null, null, null, [])); }
        public Task TriggerAsync(Guid id, CancellationToken ct = default) { Writes++; return Task.CompletedTask; }
        public Task<AiAnomalyRuleResponse> SetEnabledAsync(Guid id, AiAnomalyChangeRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CloseAsync(Guid id, byte[] rowVersion, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
