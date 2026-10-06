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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Api.Controllers;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.AiEvaluations;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class AiScenarioApiTests
{
    [Theory]
    [InlineData(false, null, HttpStatusCode.Unauthorized)]
    [InlineData(true, AiCenterConstants.ChatUsePermission, HttpStatusCode.Forbidden)]
    [InlineData(true, AiCenterConstants.GovernanceViewPermission, HttpStatusCode.OK)]
    public async Task List_ShouldKeepAuthenticationAndGovernancePermission(bool authenticated, string? permission, HttpStatusCode expected)
    {
        var service = new ContractService(); using var host = await Server(service); using var client = host.GetTestClient();
        Authenticate(client, authenticated, permission);
        using var response = await client.GetAsync("/api/ai/scenarios");
        Assert.Equal(expected, response.StatusCode); Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, service.Calls);
    }

    [Fact]
    public async Task Options_ShouldUseChatPermissionWithoutGrantingManagement()
    {
        var service = new ContractService(); using var host = await Server(service); using var client = host.GetTestClient();
        Authenticate(client, true, AiCenterConstants.ChatUsePermission);
        using var options = await client.GetAsync("/api/ai/scenarios/options"); Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        using var save = await client.PutAsync("/api/ai/scenarios", Json("{}")); Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);
        Assert.Equal(1, service.Calls);
    }

    [Theory]
    [InlineData("{\"tenantId\":\"10000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"configuration\":{\"disableAuthorization\":true}}")]
    [InlineData("{\"configuration\":{\"maxModelRounds\":\"6\"}}")]
    public async Task Save_ShouldRejectIdentityOverridesAndUnpublishedConfiguration(string json)
    {
        var service = new ContractService(); using var host = await Server(service); using var client = host.GetTestClient();
        Authenticate(client, true, AiCenterConstants.GovernanceManagePermission);
        using var response = await client.PutAsync("/api/ai/scenarios", Json(json));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task Report_ShouldReturnEvaluationEnumNamesForHumanReviewUi()
    {
        var service = new ContractService(); using var host = await Server(service); using var client = host.GetTestClient();
        Authenticate(client, true, AiCenterConstants.GovernanceViewPermission);
        using var response = await client.GetAsync($"/api/ai/scenarios/evaluations/{Guid.NewGuid()}/report");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        Assert.Equal("Live", data.GetProperty("mode").GetString());
        Assert.Equal("NotApplicable", data.GetProperty("results")[0].GetProperty("status").GetString());
    }

    [Fact]
    public void MutatingEndpoints_ShouldCarryExistingManagePolicyAndIdempotencyMetadata()
    {
        foreach (var name in new[] { "Save", "Freeze", "Copy", "Import", "Review", "Publish", "Rollback", "Stop", "Revoke" })
        {
            var method = typeof(AiScenarioController).GetMethod(name)!;
            Assert.Equal(AiCenterConstants.GovernanceManagePermission, method.GetCustomAttributes(typeof(PermissionAttribute), false).Cast<PermissionAttribute>().Single().PermissionCode);
            Assert.Single(method.GetCustomAttributes(typeof(IdempotencyKeyAttribute), false));
        }
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");
    private static void Authenticate(HttpClient client, bool authenticated, string? permission)
    { if (authenticated) client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true"); if (permission is not null) client.DefaultRequestHeaders.Add("X-Test-Permission", permission); }
    private static Task<IHost> Server(ContractService service) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
    {
        services.AddHttpContextAccessor(); services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        services.AddAuthorization(); services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>(); services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IAiScenarioService>(service); services.AddControllers().AddApplicationPart(typeof(AiScenarioController).Assembly);
    }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).StartAsync();
    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-Authenticated"] != "true") return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity("Test"); foreach (var permission in Request.Headers["X-Test-Permission"]) identity.AddClaim(new(ClaimConstants.PermissionCode, permission!));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), "Test")));
        }
    }
    private sealed class ContractService : IAiScenarioService
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<AiScenarioResponse>> ListAsync(CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult<IReadOnlyList<AiScenarioResponse>>([]); }
        public Task<IReadOnlyList<AiScenarioOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult<IReadOnlyList<AiScenarioOption>>([]); }
        public Task<AiScenarioResponse> SaveAsync(SaveAiScenarioRequest request, CancellationToken cancellationToken = default)
        { Calls++; throw new NotSupportedException(); }
        public Task<AiScenarioDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiScenarioVersionResponse> FreezeAsync(Guid id, AiScenarioChangeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiScenarioResponse> CopyToDraftAsync(Guid id, AiScenarioChangeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiScenarioSnapshot> ExportAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiScenarioEvaluationResponse> ImportAsync(Guid id, ImportAiScenarioEvaluationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvaluationReport> GetEvaluationReportAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(new EvaluationReport
        {
            SuiteHash = "synthetic", FixtureHash = "synthetic", CheckerHash = "synthetic", SourceHash = "synthetic", Mode = EvaluationMode.Live,
            Results = [new() { Key = "synthetic/offline-only", CaseHash = "synthetic", Status = EvaluationStatus.NotApplicable }]
        });
        public Task ReviewAsync(Guid id, ReviewAiScenarioEvaluationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PublishAsync(Guid id, AiScenarioChangeRequest request, bool rollback = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task StopAsync(Guid id, AiScenarioChangeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeEvaluationAsync(Guid id, AiScenarioChangeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiScenarioVersion> ResolveCurrentAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiScenarioSnapshot> ValidateAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ValidateModelAsync(Guid id, AiProviderConfig provider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ValidateToolAsync(Guid id, string toolCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
