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
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiActions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic009ActionApiTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string[] Permissions = [AiCenterConstants.DocumentDraftPermission,
        AiCenterConstants.DocumentExecutePermission, "demo-business-order:create"];

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("no-draft", HttpStatusCode.Forbidden)]
    [InlineData("no-execute", HttpStatusCode.Forbidden)]
    [InlineData("no-business", HttpStatusCode.Forbidden)]
    [InlineData("other-tenant", HttpStatusCode.Forbidden)]
    [InlineData("allowed", HttpStatusCode.OK)]
    public async Task Confirmation_EnforcesPublicAndPerActionAuthorization(string identity, HttpStatusCode expected)
    {
        var state = new State(); using var host = await Server(state); using var client = host.GetTestClient();
        Authenticate(client, identity);
        using var response = await Post(client, state, "confirmation");
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, state.Confirmations);
    }

    [Theory]
    [InlineData("confirmation")]
    [InlineData("execute")]
    [InlineData("cancel")]
    public async Task CachedReplay_RechecksBusinessPermissionBeforeReturningBody(string action)
    {
        var state = new State(); using var host = await Server(state); using var client = host.GetTestClient(); Authenticate(client);
        using var first = await Post(client, state, action);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var allowedReplay = await Post(client, state, action);
        Assert.Equal(HttpStatusCode.OK, allowedReplay.StatusCode);
        Assert.Equal(1, state.Actions);
        Assert.Equal(2, state.AccessChecks);
        Authenticate(client, "no-business");
        using var deniedReplay = await Post(client, state, action);
        Assert.Equal(HttpStatusCode.Forbidden, deniedReplay.StatusCode);
        Assert.DoesNotContain("synthetic-business-result", await deniedReplay.Content.ReadAsStringAsync());
        Assert.Equal(1, state.Actions);
        Assert.Equal(3, state.AccessChecks);
    }

    [Theory]
    [InlineData("actor", HttpStatusCode.NotFound)]
    [InlineData("type", HttpStatusCode.Forbidden)]
    [InlineData("version", HttpStatusCode.Forbidden)]
    public async Task CachedExecution_RechecksDraftBinding(string changed, HttpStatusCode expected)
    {
        var state = new State(); using var host = await Server(state); using var client = host.GetTestClient(); Authenticate(client);
        using var first = await Post(client, state, "execute"); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        switch (changed)
        {
            case "actor": state.ActorId = Guid.NewGuid(); break;
            case "type": state.BusinessType = "UnknownOrder"; break;
            case "version": state.Version = "99.0"; break;
        }
        using var replay = await Post(client, state, "execute");
        Assert.Equal(expected, replay.StatusCode);
        Assert.Equal(1, state.Actions);
        Assert.DoesNotContain("synthetic-business-result", await replay.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, State state, string action) => client.PostAsync(
        $"/api/ai/document-drafts/{state.DraftId}/{action}", new StringContent("{}", Encoding.UTF8, "application/json"));

    private static void Authenticate(HttpClient client, string scenario = "allowed")
    {
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", "synthetic-action-key");
        if (scenario == "anonymous") return;
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        foreach (var permission in Permissions.Where(code => scenario switch
        {
            "no-draft" => code != AiCenterConstants.DocumentDraftPermission,
            "no-execute" => code != AiCenterConstants.DocumentExecutePermission,
            "no-business" => code != "demo-business-order:create",
            _ => true
        })) client.DefaultRequestHeaders.Add("X-Test-Permission", permission);
        if (scenario == "other-tenant") client.DefaultRequestHeaders.Add("X-Test-Tenant", Guid.NewGuid().ToString());
    }

    private static Task<IHost> Server(State state) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
        .ConfigureServices(services =>
        {
            services.AddHttpContextAccessor();
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
            services.AddAuthorization();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            var tenant = new TenantContext(); tenant.SetTenant(TenantId, "synthetic-test");
            services.AddSingleton<ITenantContext>(tenant);
            services.AddSingleton<IAiCenterConfiguration>(new Configuration());
            services.AddScoped<AiBusinessActionAccessPolicy>();
            services.AddSingleton<IAiBusinessActionHandler, DefinitionHandler>();
            services.AddScoped<IAiActionToolRegistry, AiActionToolRegistry>();
            services.AddSingleton(state);
            services.AddScoped<ContractService>();
            services.AddScoped<IAiDocumentDraftService>(p => p.GetRequiredService<ContractService>());
            services.AddScoped<IAiDocumentExecutionService>(p => p.GetRequiredService<ContractService>());
            services.AddSingleton<IIdempotencyService, MemoryIdempotency>();
            services.AddScoped<IdempotencyFilter>();
            services.AddScoped<PreventDuplicateSubmitFilter>();
            services.AddControllers(options =>
            {
                options.Filters.AddService<IdempotencyFilter>();
                options.Filters.AddService<PreventDuplicateSubmitFilter>();
            }).AddApplicationPart(typeof(AiDocumentDraftController).Assembly);
        }).Configure(app =>
        {
            app.UseMiddleware<GlobalExceptionMiddleware>(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        })).StartAsync();

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-Authenticated"] != "true") return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity("Test");
            identity.AddClaim(new(ClaimConstants.UserId, UserId.ToString()));
            identity.AddClaim(new(ClaimConstants.TenantId, Request.Headers["X-Test-Tenant"].FirstOrDefault() ?? TenantId.ToString()));
            foreach (var code in Request.Headers["X-Test-Permission"]) identity.AddClaim(new(ClaimConstants.PermissionCode, code!));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), "Test")));
        }
    }

    private sealed class Configuration : IAiCenterConfiguration
    {
        public bool Enabled => true;
        public IReadOnlyCollection<Guid> AllowedTenantIds => [TenantId];
        public int ConversationRetentionDays => 30;
        public int AuditRetentionDays => 180;
    }

    private sealed class DefinitionHandler : IAiBusinessActionHandler
    {
        public AiBusinessActionDefinition Definition => DemoBusinessOrderDraftHandler.ActionDefinition;
        public Task<AiActionToolExecutionResult> PrepareDraftAsync(AiActionDraftContext context, string argumentsJson, CancellationToken token = default) =>
            throw new InvalidOperationException("HTTP contract fixture cannot prepare business drafts.");
    }

    private sealed class State
    {
        public Guid DraftId { get; } = Guid.NewGuid();
        public Guid ActorId { get; set; } = UserId;
        public string BusinessType { get; set; } = "DemoBusinessOrder";
        public string Version { get; set; } = "1.0";
        public int AccessChecks { get; set; }
        public int Actions { get; set; }
        public int Confirmations { get; set; }
    }

    private sealed class ContractService(State state, AiBusinessActionAccessPolicy policy, IAiActionToolRegistry registry)
        : IAiDocumentDraftService, IAiDocumentExecutionService
    {
        private AiBusinessActionDefinition Check(Guid id)
        {
            state.AccessChecks++;
            var identity = policy.EnsureIdentity();
            if (id != state.DraftId || identity.UserId != state.ActorId)
                throw new BusinessException(ErrorCode.NotFound, "Synthetic draft was not found.");
            return registry.FindDefinition(state.BusinessType, state.Version) ??
                throw new BusinessException(ErrorCode.Forbidden, "Synthetic action is unsupported.");
        }
        public Task EnsureAccessAsync(Guid id, CancellationToken token = default)
        { policy.EnsureExecute(Check(id)); return Task.CompletedTask; }
        public Task<AiDocumentDraftResponse> GetByIdAsync(Guid id, CancellationToken token = default)
        { policy.EnsurePrepare(Check(id)); return Task.FromResult(new AiDocumentDraftResponse { Id = id }); }
        public Task<AiDocumentConfirmationResponse> ConfirmAsync(Guid id, CreateAiDocumentConfirmationRequest request, CancellationToken token = default)
        { state.Actions++; state.Confirmations++; return Task.FromResult(new AiDocumentConfirmationResponse { Id = Guid.NewGuid(), DraftId = id }); }
        public Task<AiDocumentExecutionResponse> ExecuteAsync(Guid id, ExecuteAiDocumentDraftRequest request, CancellationToken token = default)
        { state.Actions++; return Task.FromResult(new AiDocumentExecutionResponse { DraftId = id, BusinessNo = "synthetic-business-result" }); }
        public Task<AiDocumentDraftResponse> CancelAsync(Guid id, CancelAiDocumentDraftRequest request, CancellationToken token = default)
        { state.Actions++; return Task.FromResult(new AiDocumentDraftResponse { Id = id }); }
        public Task<AiBusinessActionSchemaResponse> GetDemoBusinessOrderSchemaAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiDocumentDraftResponse>> GetByConversationAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiDocumentDraftResponse>> GetByRunAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
        public Task<AiDocumentDraftResponse> UpdateAsync(Guid id, UpdateAiDocumentDraftRequest request, CancellationToken token = default) => throw new NotSupportedException();
    }

    private sealed class MemoryIdempotency : IIdempotencyService
    {
        private readonly Dictionary<string, IdempotencyCacheEntry> _entries = [];
        public Task<IdempotencyCacheEntry?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(_entries.GetValueOrDefault(key));
        public Task<bool> TryBeginAsync(string key, IdempotencyCacheEntry entry, TimeSpan expiry, CancellationToken token = default) =>
            Task.FromResult(_entries.TryAdd(key, entry));
        public Task<bool> StoreAsync(string key, string operationId, IdempotencyCacheEntry entry, TimeSpan expiry, CancellationToken token = default)
        { _entries[key] = entry; return Task.FromResult(true); }
        public Task RemoveAsync(string key, string? operationId = null, CancellationToken token = default)
        { _entries.Remove(key); return Task.CompletedTask; }
        public Task<bool> TryAcquireDuplicateSubmitLockAsync(string key, TimeSpan expiry, CancellationToken token = default) => Task.FromResult(true);
    }
}
