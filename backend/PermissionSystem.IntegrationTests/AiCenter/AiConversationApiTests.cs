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
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class AiConversationApiTests
{
    [Theory]
    [InlineData(false, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, HttpStatusCode.Forbidden)]
    [InlineData(true, true, HttpStatusCode.Accepted)]
    public async Task AsyncSubmission_ShouldReturn202AndKeepAuthorization(bool authenticated, bool allowed, HttpStatusCode expected)
    {
        var service = new ContractService();
        using var host = await CreateServerAsync(service, true);
        using var client = host.GetTestClient();
        if (authenticated) client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        if (allowed) client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.ChatUsePermission);
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "synthetic-key");
        using var response = await client.PostAsync($"/api/ai/conversations/{Guid.NewGuid()}/runs", Json("{\"content\":\"新查询\"}"));
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.Accepted ? 1 : 0, service.CallCount);
        if (expected == HttpStatusCode.Accepted)
        {
            Assert.Equal("synthetic-key", service.SubmissionKey);
            Assert.NotNull(response.Headers.Location);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(1, body.RootElement.GetProperty("data").GetProperty("status").GetInt32());
            Assert.DoesNotContain("actorSessionId", body.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
    [Theory]
    [InlineData(false, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, HttpStatusCode.Forbidden)]
    [InlineData(true, true, HttpStatusCode.OK)]
    public async Task MessageEndpoint_ShouldKeepAuthenticationAndPermissionPolicy(bool authenticated, bool allowed, HttpStatusCode expected)
    {
        var service = new ContractService();
        using var host = await CreateServerAsync(service);
        using var client = host.GetTestClient();
        if (authenticated) client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        if (allowed) client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.ChatUsePermission);
        using var response = await client.PostAsync($"/api/ai/conversations/{Guid.NewGuid()}/messages", Json("{\"content\":\"新查询\"}"));
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, service.CallCount);
        if (expected == HttpStatusCode.OK) Assert.Null(service.Request!.ContextRef);
    }

    [Fact]
    public async Task MessageEndpoint_ShouldBindReferenceAndExplicitTimezoneAndSerializeStructuredResults()
    {
        var service = new ContractService();
        using var host = await CreateServerAsync(service);
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.ChatUsePermission);
        var reference = new AiContextReference(Guid.NewGuid(), "query-1");
        using var response = await client.PostAsync($"/api/ai/conversations/{Guid.NewGuid()}/messages",
            Json(JsonSerializer.Serialize(new { content = "再看上个月", contextRef = reference, utcOffsetMinutes = 480 }, AiStructuredResults.JsonOptions)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(reference, service.Request!.ContextRef);
        Assert.Equal(480, service.Request.UtcOffsetMinutes);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = body.RootElement.GetProperty("data").GetProperty("structuredResults")[0];
        Assert.Equal("table", result.GetProperty("type").GetString());
        Assert.Equal(100, result.GetProperty("table").GetProperty("totalCount").GetInt64());
        Assert.Equal(0, result.GetProperty("table").GetProperty("displayedRowCount").GetInt32());
        Assert.True(result.GetProperty("isTruncated").GetBoolean());
        Assert.False(body.RootElement.GetProperty("data").GetProperty("structuredResultsUnavailable").GetBoolean());
    }

    [Theory]
    [InlineData("{\"content\":\"query\",\"tenantId\":\"10000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"content\":\"query\",\"contextRef\":{\"runId\":\"invalid\",\"invocationId\":\"x\"}}")]
    [InlineData("{\"content\":\"query\",\"contextRef\":{\"runId\":\"10000000-0000-0000-0000-000000000001\",\"invocationId\":\"x\",\"actorUserId\":\"fake\"}}")]
    [InlineData("{\"content\":\"query\",\"utcOffsetMinutes\":\"480\"}")]
    public async Task MessageEndpoint_ShouldRejectUnpublishedFieldsAndMalformedBindings(string json)
    {
        var service = new ContractService();
        using var host = await CreateServerAsync(service);
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.ChatUsePermission);
        using var response = await client.PostAsync($"/api/ai/conversations/{Guid.NewGuid()}/messages", Json(json));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, service.CallCount);
    }

    private static StringContent Json(string content) => new(content, Encoding.UTF8, "application/json");

    private static Task<IHost> CreateServerAsync(ContractService service, bool submissions = false) => new HostBuilder().ConfigureWebHost(web =>
        web.UseTestServer().ConfigureServices(services =>
        {
            services.AddHttpContextAccessor();
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            services.AddAuthorization();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IAiConversationService>(service);
            if (submissions) services.AddSingleton<IAiRunSubmissionService>(service);
            services.AddControllers().AddApplicationPart(typeof(AiConversationController).Assembly);
        }).Configure(app =>
        {
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        })).StartAsync();

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-Authenticated"] != "true") return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity("Test");
            foreach (var code in Request.Headers["X-Test-Permission"]) identity.AddClaim(new(ClaimConstants.PermissionCode, code!));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), "Test")));
        }
    }

    private sealed class ContractService : IAiConversationService, IAiRunSubmissionService
    {
        public string? SubmissionKey { get; private set; }
        public Task<AiRunResponse> SubmitAsync(Guid id, SendAiMessageRequest request, string key, CancellationToken token = default)
        {
            CallCount++; Request = request; SubmissionKey = key;
            return Task.FromResult(new AiRunResponse { Id = Guid.NewGuid(), ConversationId = id, Status = PermissionSystem.Domain.Enums.AiRunStatus.Pending });
        }
        public Task<AiRunResponse> SubmitRetryAsync(Guid id, string key, CancellationToken token = default) => throw new NotSupportedException();
        public Task<AiRunResponse> WaitAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
        public Task<AiRunResponse> GetSubmissionAsync(Guid id, string key, CancellationToken token = default) => throw new NotSupportedException();
        public int CallCount { get; private set; }
        public SendAiMessageRequest? Request { get; private set; }
        public Task<AiRunResponse> SendMessageAsync(Guid conversationId, SendAiMessageRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++; Request = request;
            return Task.FromResult(new AiRunResponse { ConversationId = conversationId, StructuredResults =
            [new() { Type = "table", Table = new() { TotalCount = 100 }, IsTruncated = true }] });
        }
        public Task<PagedResult<AiConversationListResponse>> GetPagedAsync(AiConversationQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiConversationDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiConversationDetailResponse> CreateAsync(CreateAiConversationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiRunResponse> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiToolCitation>> GetCitationsAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelRunAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiRunResponse> RetryRunAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
