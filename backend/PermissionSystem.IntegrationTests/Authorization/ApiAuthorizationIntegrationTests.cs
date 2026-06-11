using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.IntegrationTests.Authorization;

public sealed class ApiAuthorizationIntegrationTests
{
    [Fact]
    public async Task ProtectedEndpoint_ShouldReturn401_WhenUnauthenticated()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PermissionEndpoint_ShouldReturn403_WhenPermissionIsMissing()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "normal-admin");
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "system:role:view");

        var response = await client.GetAsync("/system/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PermissionEndpoint_ShouldReturn200_WhenPermissionIsGranted()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "operator");
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "system:user:view");

        var response = await client.GetAsync("/system/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemManagementEndpoint_ShouldRejectNormalUserWithoutPermission()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "normal-user");

        var response = await client.GetAsync("/system/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static TestServer CreateServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddHttpContextAccessor();
                services.AddRouting();
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                services.AddAuthorization();
                services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
                services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
                services.AddScoped<ICurrentUserService, HeaderCurrentUserService>();
                services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationMiddlewareResultHandler>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/protected", () => "ok")
                        .RequireAuthorization();
                    endpoints.MapGet("/system/users", () => "ok")
                        .RequireAuthorization(new PermissionAttribute("system:user:view"));
                });
            });

        return new TestServer(builder);
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userName = Request.Headers["X-Test-User"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimConstants.Username, userName)],
                Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class HeaderCurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HeaderCurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
        public Guid? UserId => IsAuthenticated ? Guid.Parse("30000000-0000-0000-0000-000000000001") : null;
        public Guid? TenantId => Guid.Parse("10000000-0000-0000-0000-000000000001");
        public Guid? DepartmentId => null;
        public string? SessionId => "test";
        public string? Username => _httpContextAccessor.HttpContext?.Request.Headers["X-Test-User"].FirstOrDefault();
        public IReadOnlyCollection<string> Roles => IsSuperAdmin ? [ClaimConstants.SuperAdminRoleCode] : [];
        public IReadOnlyCollection<string> PermissionCodes => ReadPermissions();
        public bool IsSuperAdmin => string.Equals(
            _httpContextAccessor.HttpContext?.Request.Headers["X-Test-SuperAdmin"].FirstOrDefault(),
            "true",
            StringComparison.OrdinalIgnoreCase);

        public bool IsCurrentUserSuperAdmin() => IsSuperAdmin;
        public bool IsCurrentUserAdmin() => IsSuperAdmin;
        public bool CanManageBuiltinResources() => IsSuperAdmin;

        public bool HasPermission(string permissionCode)
        {
            return IsSuperAdmin || ReadPermissions().Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
        }

        private IReadOnlyCollection<string> ReadPermissions()
        {
            var raw = _httpContextAccessor.HttpContext?.Request.Headers["X-Test-Permissions"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(raw)
                ? []
                : raw.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
