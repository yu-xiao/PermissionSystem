using System.Net;
using System.Security.Claims;
using System.Text;
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
using PermissionSystem.Application.Reports;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic006ReportApiTests
{
    [Theory]
    [InlineData(false, "report:view", HttpStatusCode.Unauthorized)]
    [InlineData(true, "system:user:view", HttpStatusCode.Forbidden)]
    [InlineData(true, "report:view", HttpStatusCode.OK)]
    public async Task Query_UsesExistingPermissionPolicy(bool authenticated, string permission, HttpStatusCode expected)
    {
        var service = new ContractService(); using var host = await Server(service); using var client = host.GetTestClient();
        if (authenticated) client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", permission);
        using var response = await client.PostAsync($"/api/reports/{Guid.NewGuid()}/query", Json("""{"mode":"Metrics","dimension":"DepartmentId","params":{"startTime":"2026-10-01T00:00:00+08:00"},"limit":1}"""));
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, service.Calls);
        if (expected == HttpStatusCode.OK)
        {
            Assert.Equal("Metrics", service.Request!.Mode);
            Assert.Equal("2026-10-01T00:00:00+08:00", service.Request.Params["startTime"].GetString());
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"totalCount\":5000", content); Assert.Contains("\"rowCount\":0", content);
            Assert.Contains("\"unit\":", content);
        }
    }

    [Theory]
    [InlineData("{\"actorUserId\":\"untrusted\"}")]
    [InlineData("{\"params\":{\"keyword\":\"a\",\"Keyword\":\"b\"}}")]
    [InlineData("{\"mode\":\"Rows\",\"Mode\":\"Metrics\"}")]
    [InlineData("{\"limit\":\"1\"}")]
    [InlineData("{\"params\":null}")]
    public async Task Query_RejectsUnknownAmbiguousOrMalformedPayloadBeforeApplication(string payload)
    {
        var service = new ContractService(); using var host = await Server(service); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true"); client.DefaultRequestHeaders.Add("X-Test-Permission", "report:view");
        using var response = await client.PostAsync($"/api/reports/{Guid.NewGuid()}/query", Json(payload));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, service.Calls);
    }

    private static StringContent Json(string content) => new(content, Encoding.UTF8, "application/json");
    private static Task<IHost> Server(ContractService service) => new HostBuilder().ConfigureWebHost(web =>
        web.UseTestServer().ConfigureServices(services =>
        {
            services.AddHttpContextAccessor(); services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, Authentication>("Test", _ => { });
            services.AddAuthorization(); services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>(); services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IReportService>(service); services.AddControllers().AddApplicationPart(typeof(ReportController).Assembly);
        }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(endpoints => endpoints.MapControllers()); })).StartAsync();

    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-Authenticated"] != "true") return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity("Test"); foreach (var code in Request.Headers["X-Test-Permission"]) identity.AddClaim(new(ClaimConstants.PermissionCode, code!));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), "Test")));
        }
    }

    private sealed class ContractService : IReportService
    {
        public int Calls { get; private set; }
        public ReportQueryRequest? Request { get; private set; }
        public Task<ReportQueryResponse> QueryAsync(Guid id, ReportQueryRequest request, CancellationToken cancellationToken = default)
        {
            Calls++; Request = request;
            return Task.FromResult(new ReportQueryResponse { TotalCount = 5000, Metrics = new() { Totals = new(5000, 4000, 1000) }, IsTruncated = true });
        }
        public Task<PagedResult<ReportDefinitionResponse>> GetPagedAsync(ReportDefinitionQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ReportDefinitionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ReportDefinitionResponse> CreateAsync(CreateReportDefinitionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ReportDefinitionResponse> UpdateAsync(Guid id, UpdateReportDefinitionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReportDatasetResponse>> GetDatasetsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportAsync(Guid id, ReportQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<ReportExecutionLogResponse>> GetExecutionLogsAsync(ReportExecutionLogQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
