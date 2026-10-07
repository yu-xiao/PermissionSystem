using System.Net;
using System.Security.Claims;
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
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic012OperationsApiTests
{
    [Theory]
    [InlineData(false, "ai:operations:view", HttpStatusCode.Unauthorized)]
    [InlineData(true, "ai:chat:use", HttpStatusCode.Forbidden)]
    [InlineData(true, "ai:operations:view", HttpStatusCode.OK)]
    public async Task ScenarioRoute_UsesOriginalPermissionAndNoStore(bool authenticated, string permission, HttpStatusCode expected)
    {
        var state = new ContractService(); using var host = await Server(state); using var client = host.GetTestClient();
        if (authenticated) client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", permission);
        using var response = await client.GetAsync("/api/ai/operations/scenarios");
        Assert.Equal(expected, response.StatusCode); Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, state.ScenarioCalls);
        if (expected == HttpStatusCode.OK)
        {
            Assert.True(response.Headers.CacheControl?.NoStore);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("succeeded").GetBoolean());
            var data = json.RootElement.GetProperty("data"); Assert.Equal(1, data.GetProperty("metricsVersion").GetInt32());
            Assert.Equal(10000, data.GetProperty("limits").GetProperty("maxRuns").GetInt32());
            Assert.Equal(0, data.GetProperty("scenarios").GetProperty("totalCount").GetInt64());
        }
    }

    [Fact]
    public async Task ScenarioRoute_BindsDatesAndPaginationWithoutUserOrTenantArguments()
    {
        var state = new ContractService(); using var host = await Server(state); using var client = host.GetTestClient();
        Authenticate(client);
        var from = "2026-10-01T00:00:00+08:00"; var to = "2026-10-02T00:00:00+08:00";
        using var response = await client.GetAsync($"/api/ai/operations/scenarios?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}&pageIndex=2&pageSize=10&tenantId={Guid.NewGuid()}&actorUserId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DateTimeOffset.Parse(from), state.Request!.From); Assert.Equal(DateTimeOffset.Parse(to), state.Request.To);
        Assert.Equal(2, state.Request.PageIndex); Assert.Equal(10, state.Request.PageSize);
        Assert.DoesNotContain(typeof(AiScenarioOperationsQueryRequest).GetProperties(), p => p.Name is "TenantId" or "ActorUserId");
    }

    [Theory]
    [InlineData("pageSize=not-a-number")]
    [InlineData("from=not-a-date")]
    public async Task ScenarioRoute_RejectsMalformedDtoBeforeApplication(string query)
    {
        var state = new ContractService(); using var host = await Server(state); using var client = host.GetTestClient(); Authenticate(client);
        using var response = await client.GetAsync($"/api/ai/operations/scenarios?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, state.ScenarioCalls);
    }

    [Fact]
    public async Task ExistingSummaryRoute_KeepsItsResponseContract()
    {
        var state = new ContractService(); using var host = await Server(state); using var client = host.GetTestClient(); Authenticate(client);
        using var response = await client.GetAsync("/api/ai/operations/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(1, state.SummaryCalls); Assert.Equal(0, state.ScenarioCalls);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, json.RootElement.GetProperty("data").GetProperty("runCount").GetInt64());
    }

    private static void Authenticate(HttpClient client)
    { client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true"); client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.OperationsViewPermission); }
    internal static Task<IHost> Server(ContractService state) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
        .ConfigureServices(services =>
        {
            services.AddHttpContextAccessor(); services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
            services.AddAuthorization(); services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>(); services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IAiScenarioOperationsService>(state); services.AddSingleton<IAiOperationsService>(state);
            services.AddSingleton<IAiTechnicalExportService>(state);
            services.AddSingleton<IAiTechnicalExportReceiptService>(state);
            services.AddSingleton<IAiCostQualityService>(state);
            services.AddControllers().AddApplicationPart(typeof(AiOperationsController).Assembly);
        }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).StartAsync();
    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-Authenticated"] != "true") return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity("Test"); foreach (var code in Request.Headers["X-Test-Permission"]) identity.AddClaim(new(ClaimConstants.PermissionCode, code!));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), "Test")));
        }
    }
    internal sealed class ContractService : IAiScenarioOperationsService, IAiOperationsService, IAiTechnicalExportService, IAiTechnicalExportReceiptService, IAiCostQualityService
    {
        public int ScenarioCalls { get; private set; }
        public int SummaryCalls { get; private set; }
        public int ExportCalls { get; private set; }
        public AiTechnicalExportRequest? ExportRequest { get; private set; }
        public AiScenarioOperationsQueryRequest? Request { get; private set; }
        public int ReceiptCalls { get; private set; }
        public AiTechnicalExportReceiptWindow? ReceiptRequest { get; private set; }
        public Guid? ReceiptExportId { get; private set; }
        public int CostQualityCalls { get; private set; }
        public AiCostQualityQuery? CostQualityRequest { get; private set; }
        public int CostQualityTrendCalls { get; private set; }
        public AiCostQualityTrendQuery? CostQualityTrendRequest { get; private set; }
        public Task<AiCostQualityTrendResponse> QueryTrendAsync(AiCostQualityTrendQuery request, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); CostQualityTrendCalls++; CostQualityTrendRequest = request;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new AiCostQualityTrendResponse(Guid.NewGuid(), request.From ?? now.AddDays(-30), request.To ?? now,
                now, now, new(0, 0, 0, 0, 0, 0, 0, 0), new(0, 0, 0, 0), [],
                new(0, 0, 0, 0, 0, null, 0, 0), new(0, 0, 0, 0, 0, null, 0, 0), new(0, 0), [],
                PagedResult<AiCostQualityCurrencyDistribution>.Create([], request.PageIndex, request.PageSize, 0)));
        }
        public Task<AiCostQualityResponse> QueryAsync(AiCostQualityQuery request, CancellationToken ct = default)
        {
            CostQualityCalls++; CostQualityRequest = request;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new AiCostQualityResponse(Guid.NewGuid(), request.From ?? now.AddDays(-30), request.To ?? now,
                now, now, new(0, 0, 0, 0, 0, 0, 0, 0), new(0, 0, 0, 0), [],
                new(0, 0, 0, 0, 0, null, 0, 0), new(0, 0, 0, 0, 0, null, 0, 0), new(0, 0),
                PagedResult<AiCostQualityCurrencySummary>.Create([], request.PageIndex, request.PageSize, 0)));
        }
        public Task<AiTechnicalExportReceiptPage> QueryAsync(AiTechnicalExportReceiptQuery request, CancellationToken ct = default)
        {
            ReceiptCalls++; ReceiptRequest = request;
            return Task.FromResult(new AiTechnicalExportReceiptPage(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0, 0, PagedResult<AiTechnicalExportReceiptSummary>.Create([], request.PageIndex, request.PageSize, 0)));
        }
        public Task<AiTechnicalExportReceiptDetail> GetAsync(Guid exportId, AiTechnicalExportReceiptWindow request, CancellationToken ct = default)
        {
            ReceiptCalls++; ReceiptRequest = request; ReceiptExportId = exportId;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new AiTechnicalExportReceiptDetail(Guid.NewGuid(), now.AddDays(-30), now, now, now, 0, 0,
                new(exportId, now, now, 0, 0, 0, false, "MissingPrepared"), []));
        }
        public Task<AiScenarioOperationsResponse> QueryAsync(AiScenarioOperationsQueryRequest request, CancellationToken ct = default)
        {
            ScenarioCalls++; Request = request;
            return Task.FromResult(new AiScenarioOperationsResponse
            { Scenarios = PagedResult<AiScenarioOperationsItemResponse>.Create([], request.PageIndex, request.PageSize, 0) });
        }
        public Task<AiOperationsSummaryResponse> GetSummaryAsync(AiOperationsQueryRequest request, CancellationToken ct = default)
        { SummaryCalls++; return Task.FromResult(new AiOperationsSummaryResponse { RunCount = 3 }); }
        public Task<AiFeedbackResponse?> GetMyFeedbackAsync(Guid runId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiTechnicalExportFile> ExportAsync(AiTechnicalExportRequest request, CancellationToken ct = default)
        { ExportCalls++; ExportRequest = request; return Task.FromResult(new AiTechnicalExportFile(Guid.NewGuid(), "{}"u8.ToArray())); }
        public Task<AiFeedbackResponse> SaveMyFeedbackAsync(Guid runId, SaveAiFeedbackRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
