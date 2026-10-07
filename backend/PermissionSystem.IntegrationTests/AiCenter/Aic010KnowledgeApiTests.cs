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
using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.IntegrationTests.AiCenter;

// HTTP binding and original authorization policy; document ACL behavior is tested with the real application service separately.
public sealed class Aic010KnowledgeApiTests
{
    [Theory]
    [InlineData("documents", AiCenterConstants.KnowledgeViewPermission)]
    [InlineData("search?keyword=synthetic", AiCenterConstants.KnowledgeQueryPermission)]
    public async Task ReadRoutes_RequireAuthenticationPermissionAndDisableCaching(string path, string permission)
    {
        var state = new ContractService(); using var host = await Server(state); using var client = host.GetTestClient();
        using var anonymous = await client.GetAsync($"/api/ai/knowledge/{path}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        using var denied = await client.GetAsync($"/api/ai/knowledge/{path}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Equal(0, state.Reads);
        client.DefaultRequestHeaders.Add("X-Test-Permission", permission);
        using var allowed = await client.GetAsync($"/api/ai/knowledge/{path}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode); Assert.Equal(1, state.Reads);
        Assert.True(allowed.Headers.CacheControl?.NoStore);
        client.DefaultRequestHeaders.Remove("X-Test-Permission");
        using var revoked = await client.GetAsync($"/api/ai/knowledge/{path}");
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode); Assert.Equal(1, state.Reads);
    }

    [Fact]
    public async Task ManagePermission_DoesNotAuthorizeSearch()
    {
        var state = new ContractService(); using var host = await Server(state); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.KnowledgeManagePermission);
        using var denied = await client.GetAsync("/api/ai/knowledge/search?keyword=synthetic");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Equal(0, state.Reads);
    }

    [Fact]
    public async Task MultipartImport_RejectsInvalidBase64TokenBeforeApplicationCall()
    {
        var state = new ContractService(); using var host = await Server(state); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.KnowledgeManagePermission);
        using var body = new MultipartFormDataContent
        {
            { new StringContent("synthetic"), "file", "synthetic.txt" },
            { new StringContent("invalid-token"), "rowVersion" },
            { new StringContent("2026-10-07T00:00:00Z"), "validFrom" },
            { new StringContent("2026-11-07T00:00:00Z"), "validUntil" }
        };
        using var response = await client.PostAsync($"/api/ai/knowledge/documents/{Guid.NewGuid()}/versions", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, state.Imports);
    }

    private static Task<IHost> Server(ContractService state) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
        .ConfigureServices(services =>
        {
            services.AddHttpContextAccessor();
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
            services.AddAuthorization();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IAiKnowledgeService>(state);
            services.AddControllers().AddApplicationPart(typeof(AiKnowledgeController).Assembly);
        }).Configure(app =>
        { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).StartAsync();

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-Authenticated"] != "true") return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity("Test");
            identity.AddClaim(new(ClaimConstants.UserId, "11111111-1111-1111-1111-111111111111"));
            identity.AddClaim(new(ClaimConstants.TenantId, "22222222-2222-2222-2222-222222222222"));
            foreach (var code in Request.Headers["X-Test-Permission"]) identity.AddClaim(new(ClaimConstants.PermissionCode, code!));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), "Test")));
        }
    }

    private sealed class ContractService : IAiKnowledgeService
    {
        public int Reads { get; private set; }
        public int Imports { get; private set; }
        public Task<PagedResult<AiKnowledgeDocumentResponse>> ListAsync(int pageIndex, int pageSize, CancellationToken ct = default)
        { Reads++; return Task.FromResult(PagedResult<AiKnowledgeDocumentResponse>.Create([], pageIndex, pageSize, 0)); }
        public Task<AiKnowledgeSearchResult> SearchAsync(AiKnowledgeSearchRequest request, CancellationToken ct = default)
        { Reads++; return Task.FromResult(new AiKnowledgeSearchResult([], DateTimeOffset.UtcNow, false)); }
        public Task<AiKnowledgeDocumentResponse> UploadAsync(Guid id, AiKnowledgeUploadRequest request, CancellationToken ct = default)
        { Imports++; throw new NotSupportedException(); }
        public Task<AiKnowledgeDocumentResponse> CreateAsync(CreateAiKnowledgeDocumentRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiKnowledgeDocumentResponse> SetAccessAsync(Guid id, AiKnowledgeAccessRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task PublishAsync(Guid id, Guid versionId, AiKnowledgePublishRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, byte[] rowVersion, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiKnowledgeHit> ReadChunkAsync(AiKnowledgeReference reference, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiKnowledgeSearchResult> PreviewAsync(Guid id, Guid versionId, int startSequence = 1, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
