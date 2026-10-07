using System.Linq.Expressions;
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
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic007DemoReadOnlyApiTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000007");
    private static readonly Guid UserId = Guid.Parse("30000000-0000-0000-0000-000000000007");
    private static readonly string[] Permissions = [AiCenterConstants.ChatUsePermission, AiCenterConstants.ToolQueryPermission,
        DemoBusinessOrderReadOnlyContract.ViewPermission];

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("no-chat", HttpStatusCode.Forbidden)]
    [InlineData("no-business", HttpStatusCode.Forbidden)]
    [InlineData("no-tool", HttpStatusCode.Forbidden)]
    [InlineData("revoked", HttpStatusCode.Forbidden)]
    [InlineData("inactive", HttpStatusCode.Unauthorized)]
    [InlineData("disabled", HttpStatusCode.Forbidden)]
    [InlineData("other-tenant", HttpStatusCode.Forbidden)]
    public async Task ChatToReadOnlyTool_RejectsInvalidCallerBeforeSourceRead(string scenario, HttpStatusCode status)
    {
        var state = new State();
        if (scenario == "inactive") state.Active = false;
        if (scenario == "revoked") state.ServerPermissions = [AiCenterConstants.ChatUsePermission, AiCenterConstants.ToolQueryPermission];
        using var host = await Server(state, scenario != "disabled"); using var client = host.GetTestClient();
        Authenticate(client, scenario);
        using var response = await Send(client, "{}");
        Assert.Equal(status, response.StatusCode); Assert.Equal(0, state.ReadCalls);
    }

    [Fact]
    public async Task ChatToReadOnlyTool_SerializesSafeDemoContractWithoutCreatePermissionOrCrossTenantRows()
    {
        var state = new State(); using var host = await Server(state); using var client = host.GetTestClient();
        Authenticate(client);
        using var response = await Send(client, """{"limit":1}""");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        var result = json.RootElement.GetProperty("data").GetProperty("structuredResults")[0];
        Assert.Equal("demo-business-orders", result.GetProperty("type").GetString());
        Assert.Equal(2, result.GetProperty("demoOrders").GetProperty("totalCount").GetInt64());
        Assert.Equal(1, result.GetProperty("demoOrders").GetProperty("displayedRowCount").GetInt32());
        Assert.True(result.GetProperty("isTruncated").GetBoolean());
        Assert.Equal("demo-business-orders-readonly", result.GetProperty("citation").GetProperty("datasetCode").GetString());
        Assert.DoesNotContain("private-customer", content); Assert.DoesNotContain("foreign-order", content);
        Assert.DoesNotContain("amount", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{"tenantId":"untrusted"}""")]
    [InlineData("""{"approvalStatus":"1"}""")]
    [InlineData("""{"keyword":"a","Keyword":"b"}""")]
    [InlineData("""{"limit":201}""")]
    public async Task ChatToReadOnlyTool_RejectsUnapprovedModelArguments(string arguments)
    {
        var state = new State(); using var host = await Server(state); using var client = host.GetTestClient(); Authenticate(client);
        using var response = await Send(client, arguments);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); Assert.Equal(0, state.ReadCalls);
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string arguments) => client.PostAsync(
        $"/api/ai/conversations/{Guid.NewGuid()}/messages", new StringContent(
            JsonSerializer.Serialize(new { content = arguments }), Encoding.UTF8, "application/json"));

    private static void Authenticate(HttpClient client, string scenario = "allowed")
    {
        if (scenario == "anonymous") return;
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        foreach (var permission in Permissions.Where(code => scenario switch
        {
            "no-chat" => code != AiCenterConstants.ChatUsePermission,
            "no-business" => code != DemoBusinessOrderReadOnlyContract.ViewPermission,
            "no-tool" => code != AiCenterConstants.ToolQueryPermission,
            _ => true
        })) client.DefaultRequestHeaders.Add("X-Test-Permission", permission);
        if (scenario == "other-tenant") client.DefaultRequestHeaders.Add("X-Test-Tenant", Guid.NewGuid().ToString());
    }

    private static Task<IHost> Server(State state, bool enabled = true) => new HostBuilder().ConfigureWebHost(web =>
        web.UseTestServer().ConfigureServices(services =>
        {
            services.AddHttpContextAccessor();
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
            services.AddAuthorization(); services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>(); services.AddScoped<ICurrentUserService, CurrentUserService>();
            var tenant = new TenantContext(); tenant.SetTenant(TenantId, "synthetic-test"); services.AddSingleton<ITenantContext>(tenant);
            var options = new AiCenterOptions { Enabled = true, AllowedTenantIds = [TenantId], EnableDemoBusinessOrderQueryTool = enabled };
            services.AddSingleton<IAiCenterConfiguration>(options); services.AddSingleton<IAiToolConfiguration>(options);
            services.AddSingleton<IUserCredentialValidator>(state); services.AddSingleton<IDataScopeResolver>(state);
            services.AddSingleton<IDataScopeService>(state);
            services.AddSingleton<IAsyncQueryExecutor>(state); services.AddSingleton<IRepository<DemoBusinessOrder>>(state.Orders);
            services.AddSingleton<IRepository<Department>>(new ReadOnlyRepository<Department>([]));
            services.AddSingleton<IDataPermissionFilter, DataPermissionFilter>();
            services.AddSingleton<IDataPermissionSpecification<DemoBusinessOrder>, DemoBusinessOrderDataPermissionSpecification>();
            services.AddScoped<IDataPermissionRepository<DemoBusinessOrder>, DataPermissionRepository<DemoBusinessOrder>>();
            services.AddScoped<IAiQueryAccessGuard, AiQueryAccessGuard>();
            services.AddScoped<IDemoBusinessOrderReadOnlyQueryService, DemoBusinessOrderReadOnlyQueryService>();
            services.AddScoped<IAiReadOnlyToolHandler, DemoBusinessOrderQueryAiToolHandler>(); services.AddScoped<IAiReadOnlyToolRegistry, AiReadOnlyToolRegistry>();
            services.AddSingleton<ITraceContextAccessor, TraceContextAccessor>();
            services.AddScoped<IAiConversationService, ToolContractConversationService>();
            services.AddControllers().AddApplicationPart(typeof(AiConversationController).Assembly);
        }).Configure(app => { app.UseMiddleware<GlobalExceptionMiddleware>(); app.UseRouting(); app.UseAuthentication();
            app.UseAuthorization(); app.UseEndpoints(endpoints => endpoints.MapControllers()); })).StartAsync();

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-Authenticated"] != "true") return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity("Test"); identity.AddClaim(new(ClaimConstants.UserId, UserId.ToString()));
            identity.AddClaim(new(ClaimConstants.TenantId, Request.Headers["X-Test-Tenant"].FirstOrDefault() ?? TenantId.ToString()));
            foreach (var code in Request.Headers["X-Test-Permission"]) identity.AddClaim(new(ClaimConstants.PermissionCode, code!));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), "Test")));
        }
    }

    private sealed class State : IUserCredentialValidator, IDataScopeResolver, IDataScopeService, IAsyncQueryExecutor
    {
        public bool Active { get; set; } = true;
        public string[] ServerPermissions { get; set; } = Permissions;
        public int ReadCalls { get; private set; }
        public ReadOnlyRepository<DemoBusinessOrder> Orders { get; } = new([
            Order("one", TenantId), Order("two", TenantId), Order("foreign-order", Guid.NewGuid())]);
        private static DemoBusinessOrder Order(string suffix, Guid tenant) => new()
        { Id = Guid.NewGuid(), TenantId = tenant, CreatedBy = UserId, OrderNo = suffix, Title = suffix,
            CreatedAt = DateTimeOffset.UtcNow, CustomerName = "private-customer", Amount = 123, ApprovalStatus = ApprovalStatus.Pending };
        public Task<AuthenticatedUser?> GetAuthenticationStateAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthenticatedUser?>(Active && tenantId == TenantId && userId == UserId ?
                new(UserId, "synthetic", TenantId, null, Guid.Empty, [], ServerPermissions) : null);
        public Task<AuthenticatedUser?> ValidateAsync(string username, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid?> ResolveActiveTenantIdAsync(string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DataScopeContext> ResolveAsync(PermissionSubject subject, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DataScopeContext { CurrentUserId = UserId, ScopeType = DataScopeType.All });
        public Task<DataScopeContext> GetCurrentUserDataScopeAsync(CancellationToken cancellationToken = default) =>
            ResolveAsync(new(TenantId, UserId, null, false), cancellationToken);
        public Task<RoleDataScopeResponse> GetRoleDataScopeAsync(Guid roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetRoleDataScopeAsync(Guid roleId, SetRoleDataScopeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); ReadCalls++; return Task.FromResult<IReadOnlyList<T>>(query.ToList()); }
        public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); ReadCalls++; return Task.FromResult(query.LongCount()); }
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) => Task.FromResult(query.Any());
        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) => Task.FromResult(query.FirstOrDefault());
    }

    private sealed class ReadOnlyRepository<T>(T[] rows) : IRepository<T> where T : BaseEntity
    {
        public IQueryable<T> Query() => rows.AsQueryable();
        public IQueryable<T> QueryForTenant(Guid tenantId) => Query().Where(item => item.TenantId == tenantId);
        public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(rows.SingleOrDefault(item => item.Id == id));
        public Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<T>>(Query().Where(predicate).ToArray());
        public Task AddAsync(T entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Read-only fixture cannot write.");
        public void Update(T entity) => throw new InvalidOperationException("Read-only fixture cannot write.");
        public void Remove(T entity) => throw new InvalidOperationException("Read-only fixture cannot write.");
    }

    private sealed class ToolContractConversationService(IAiReadOnlyToolRegistry registry) : IAiConversationService
    {
        public async Task<AiRunResponse> SendMessageAsync(Guid conversationId, SendAiMessageRequest request, CancellationToken cancellationToken = default)
        {
            var output = await registry.ExecuteAsync(DemoBusinessOrderQueryAiToolHandler.ToolCode, request.Content, cancellationToken);
            return new() { ConversationId = conversationId, StructuredResults = [output.StructuredResult!] };
        }
        public Task<PagedResult<AiConversationListResponse>> GetPagedAsync(AiConversationQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiConversationDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiConversationDetailResponse> CreateAsync(CreateAiConversationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiRunResponse> GetRunAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiToolCitation>> GetCitationsAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelRunAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiRunResponse> RetryRunAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
