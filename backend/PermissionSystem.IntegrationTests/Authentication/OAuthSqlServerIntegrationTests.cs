using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Application.Users;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.IntegrationTests.Authentication;

public sealed class OAuthSqlServerIntegrationTests
{
    private const string ConnectionEnvName = "PERMISSION_SYSTEM_SQLSERVER_TEST_CONNECTION";
    private const string AdminPassword = "Admin_12345_For_Tests";
    private const string ClientSecret = "permission-admin-secret-for-tests";

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task Admin_CanLogin()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();

        var token = await RequestPasswordTokenAsync(client, "admin", AdminPassword);

        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task Login_ShouldFail_WithWrongPassword()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/connect/token", CreatePasswordGrant("admin", "wrong-password"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task RefreshToken_CanRefreshAccessToken()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        var token = await RequestPasswordTokenAsync(client, "admin", AdminPassword);

        var refreshed = await RequestRefreshTokenAsync(client, token.RefreshToken!);

        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task Revoke_ShouldMakeRefreshTokenUnusable()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        var token = await RequestPasswordTokenAsync(client, "admin", AdminPassword);

        var revokeResponse = await client.PostAsync("/connect/revoke", new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["token"] = token.RefreshToken,
            ["token_type_hint"] = "refresh_token",
            ["client_id"] = "permission-admin",
            ["client_secret"] = ClientSecret
        }));
        var refreshAfterRevoke = await client.PostAsync("/connect/token", CreateRefreshGrant(token.RefreshToken!));

        Assert.True(revokeResponse.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refreshAfterRevoke.StatusCode);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task NonDefaultTenant_WrongPassword_ShouldRecordFailureForTargetTenant()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        var identity = TestIdentity.Create();

        try
        {
            await CreateIdentityAsync(factory, identity);

            using var response = await SendPasswordGrantAsync(
                client,
                identity.UserName,
                "wrong-password",
                identity.TenantId);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertInvalidGrantAsync(response);

            using var scope = factory.Services.CreateScope();
            SetTenant(scope.ServiceProvider, identity.TenantId);
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var normalizedUserName = identity.UserName.ToUpperInvariant();
            var failureRecords = await dbContext.LoginFailureRecords
                .IgnoreQueryFilters()
                .Where(entity => entity.UserName == normalizedUserName)
                .ToListAsync();
            var loginLogs = await dbContext.LoginLogs
                .IgnoreQueryFilters()
                .Where(entity => entity.UserName == identity.UserName)
                .ToListAsync();

            Assert.Single(failureRecords);
            Assert.Equal(identity.TenantId, failureRecords[0].TenantId);
            Assert.Single(loginLogs);
            Assert.Equal(identity.TenantId, loginLogs[0].TenantId);
            Assert.Equal("Failed", loginLogs[0].LoginResult);
        }
        finally
        {
            await CleanupIdentityAsync(factory, identity);
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task NonDefaultTenant_RefreshWithoutTenantHeader_ShouldSucceed()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        var identity = TestIdentity.Create();

        try
        {
            await CreateIdentityAsync(factory, identity);
            var token = await RequestPasswordTokenAsync(
                client,
                identity.UserName,
                identity.Password,
                identity.TenantId);

            var refreshed = await RequestRefreshTokenAsync(client, token.RefreshToken!);
            var currentUser = await RequestCurrentUserAsync(client, refreshed.AccessToken);

            Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
            Assert.Equal(identity.TenantId, currentUser.TenantId);
            Assert.Equal(identity.UserId, currentUser.UserId);
        }
        finally
        {
            await CleanupIdentityAsync(factory, identity);
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task RefreshToken_ConflictingTenantHeader_ShouldUseSignedTokenTenant()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        var identity = TestIdentity.Create();

        try
        {
            await CreateIdentityAsync(factory, identity);
            var token = await RequestPasswordTokenAsync(
                client,
                identity.UserName,
                identity.Password,
                identity.TenantId);

            var refreshed = await RequestRefreshTokenAsync(
                client,
                token.RefreshToken!,
                Guid.NewGuid());
            var currentUser = await RequestCurrentUserAsync(client, refreshed.AccessToken);

            Assert.Equal(identity.TenantId, currentUser.TenantId);
            Assert.Equal(identity.UserId, currentUser.UserId);
        }
        finally
        {
            await CleanupIdentityAsync(factory, identity);
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task RefreshToken_ShouldIgnoreRevokedBearerSession()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        var identity = TestIdentity.Create();

        try
        {
            await CreateIdentityAsync(factory, identity);
            var targetToken = await RequestPasswordTokenAsync(
                client,
                identity.UserName,
                identity.Password,
                identity.TenantId);
            var targetSessionId = await GetOnlyUserSessionIdAsync(factory, identity);
            var bearerToken = await RequestPasswordTokenAsync(
                client,
                identity.UserName,
                identity.Password,
                identity.TenantId);
            await RevokeOtherUserSessionAsync(factory, identity, targetSessionId);

            var refreshed = await RequestRefreshTokenAsync(
                client,
                targetToken.RefreshToken!,
                bearerAccessToken: bearerToken.AccessToken);

            Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        }
        finally
        {
            await CleanupIdentityAsync(factory, identity);
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task RefreshToken_ShouldEnforceSignedTokenTenantIpPolicy()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString, "203.0.113.10");
        using var client = factory.CreateClient();
        var identity = TestIdentity.Create();

        try
        {
            await CreateIdentityAsync(factory, identity);
            var token = await RequestPasswordTokenAsync(
                client,
                identity.UserName,
                identity.Password,
                identity.TenantId);
            await EnableIpWhitelistAsync(factory, identity);

            using var response = await client.PostAsync(
                "/connect/token",
                CreateRefreshGrant(token.RefreshToken!));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertInvalidGrantAsync(response);
        }
        finally
        {
            await CleanupIdentityAsync(factory, identity);
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task DisabledTenant_CannotRefreshToken()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        var identity = TestIdentity.Create();

        try
        {
            await CreateIdentityAsync(factory, identity);
            var token = await RequestPasswordTokenAsync(
                client,
                identity.UserName,
                identity.Password,
                identity.TenantId);
            await SetTenantStatusAsync(factory, identity, TenantStatus.Disabled);

            using var response = await client.PostAsync(
                "/connect/token",
                CreateRefreshGrant(token.RefreshToken!));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertInvalidGrantAsync(response);
        }
        finally
        {
            await CleanupIdentityAsync(factory, identity);
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task DisabledUser_CannotRefreshToken()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        var identity = TestIdentity.Create();

        try
        {
            await CreateIdentityAsync(factory, identity);
            var token = await RequestPasswordTokenAsync(
                client,
                identity.UserName,
                identity.Password,
                identity.TenantId);
            await SetUserEnabledAsync(factory, identity, isEnabled: false);

            using var response = await client.PostAsync(
                "/connect/token",
                CreateRefreshGrant(token.RefreshToken!));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertInvalidGrantAsync(response);
        }
        finally
        {
            await CleanupIdentityAsync(factory, identity);
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task RefreshToken_ShouldUseLatestRoleAndPermissionClaims()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvName)!;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        var identity = TestIdentity.Create();

        try
        {
            await CreateIdentityAsync(factory, identity);
            var token = await RequestPasswordTokenAsync(
                client,
                identity.UserName,
                identity.Password,
                identity.TenantId);
            await ReplaceAuthorizationAsync(factory, identity);

            var refreshed = await RequestRefreshTokenAsync(client, token.RefreshToken!);
            var currentUser = await RequestCurrentUserAsync(client, refreshed.AccessToken);

            Assert.Contains(identity.UpdatedRoleCode, currentUser.Roles);
            Assert.DoesNotContain(identity.RoleCode, currentUser.Roles);
            Assert.Contains(identity.UpdatedPermissionCode, currentUser.PermissionCodes);
            Assert.DoesNotContain(identity.PermissionCode, currentUser.PermissionCodes);
        }
        finally
        {
            await CleanupIdentityAsync(factory, identity);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        string? clientIp = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Docker");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = connectionString,
                        ["ConnectionStrings:Redis"] = "localhost:6379",
                        ["Cache:Provider"] = "Memory",
                        ["Cache:EnableRedis"] = "false",
                        ["RabbitMQ:Enabled"] = "false",
                        ["RabbitMQ:EnableConsumers"] = "false",
                        ["RabbitMQ:EnableOutboxPublisher"] = "false",
                        ["RateLimit:Enabled"] = "false",
                        ["SeedData:AdminPassword"] = AdminPassword,
                        ["SeedData:OAuthClientSecret"] = ClientSecret,
                        ["Security:SystemConfigEncryptionKey"] = "0123456789abcdef0123456789abcdef",
                        ["Cors:AllowedOrigins:0"] = "http://localhost"
                    });
                });

                if (!string.IsNullOrWhiteSpace(clientIp))
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IClientIpAccessor>();
                        services.AddSingleton<IClientIpAccessor>(new FixedClientIpAccessor(clientIp));
                    });
                }
            });
    }

    private static async Task<TokenResponse> RequestPasswordTokenAsync(
        HttpClient client,
        string username,
        string password,
        Guid? tenantId = null)
    {
        using var response = await SendPasswordGrantAsync(client, username, password, tenantId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static async Task<TokenResponse> RequestRefreshTokenAsync(
        HttpClient client,
        string refreshToken,
        Guid? tenantId = null,
        string? bearerAccessToken = null)
    {
        using var response = await SendRefreshGrantAsync(
            client,
            refreshToken,
            tenantId,
            bearerAccessToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static async Task<HttpResponseMessage> SendRefreshGrantAsync(
        HttpClient client,
        string refreshToken,
        Guid? tenantId = null,
        string? bearerAccessToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = CreateRefreshGrant(refreshToken)
        };
        if (tenantId.HasValue)
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(bearerAccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerAccessToken);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendPasswordGrantAsync(
        HttpClient client,
        string username,
        string password,
        Guid? tenantId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = CreatePasswordGrant(username, password)
        };
        if (tenantId.HasValue)
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.Value.ToString());
        }

        return await client.SendAsync(request);
    }

    private static async Task<CurrentUserResponse> RequestCurrentUserAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResult<CurrentUserResponse>>();
        Assert.NotNull(result?.Data);
        return result.Data;
    }

    private static async Task AssertInvalidGrantAsync(HttpResponseMessage response)
    {
        var error = await response.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, error?.Error);
    }

    private static async Task CreateIdentityAsync(
        WebApplicationFactory<Program> factory,
        TestIdentity identity)
    {
        using var scope = factory.Services.CreateScope();
        SetTenant(scope.ServiceProvider, identity.TenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var user = new User
        {
            Id = identity.UserId,
            TenantId = identity.TenantId,
            UserName = identity.UserName,
            NormalizedUserName = identity.UserName.ToUpperInvariant(),
            DisplayName = "EA-009 integration user",
            IsEnabled = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, identity.Password);

        dbContext.Tenants.Add(new Tenant
        {
            Id = identity.TenantId,
            TenantId = identity.TenantId,
            Code = identity.TenantCode,
            Name = "EA-009 integration tenant",
            Status = TenantStatus.Active,
            StatusChangedAt = DateTimeOffset.UtcNow,
            InitializationStep = "Completed",
            InitializationProgress = 100,
            InitializedAt = DateTimeOffset.UtcNow
        });
        dbContext.Users.Add(user);
        dbContext.Roles.Add(new Role
        {
            Id = identity.RoleId,
            TenantId = identity.TenantId,
            Code = identity.RoleCode,
            Name = "EA-009 integration role",
            IsEnabled = true
        });
        dbContext.Permissions.Add(new Permission
        {
            Id = identity.PermissionId,
            TenantId = identity.TenantId,
            Code = identity.PermissionCode,
            Name = "EA-009 integration permission",
            Group = "EA-009"
        });
        dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = identity.TenantId,
            UserId = identity.UserId,
            RoleId = identity.RoleId
        });
        dbContext.RolePermissions.Add(new RolePermission
        {
            Id = Guid.NewGuid(),
            TenantId = identity.TenantId,
            RoleId = identity.RoleId,
            PermissionId = identity.PermissionId
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SetUserEnabledAsync(
        WebApplicationFactory<Program> factory,
        TestIdentity identity,
        bool isEnabled)
    {
        using var scope = factory.Services.CreateScope();
        SetTenant(scope.ServiceProvider, identity.TenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.SingleAsync(entity => entity.Id == identity.UserId);
        user.IsEnabled = isEnabled;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> GetOnlyUserSessionIdAsync(
        WebApplicationFactory<Program> factory,
        TestIdentity identity)
    {
        using var scope = factory.Services.CreateScope();
        SetTenant(scope.ServiceProvider, identity.TenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.UserSessions
            .IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId && entity.UserId == identity.UserId)
            .Select(entity => entity.Id)
            .SingleAsync();
    }

    private static async Task RevokeOtherUserSessionAsync(
        WebApplicationFactory<Program> factory,
        TestIdentity identity,
        Guid preservedSessionId)
    {
        using var scope = factory.Services.CreateScope();
        SetTenant(scope.ServiceProvider, identity.TenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sessionId = await dbContext.UserSessions
            .IgnoreQueryFilters()
            .Where(entity =>
                entity.TenantId == identity.TenantId &&
                entity.UserId == identity.UserId &&
                entity.Id != preservedSessionId)
            .Select(entity => entity.SessionId)
            .SingleAsync();
        var sessionService = scope.ServiceProvider.GetRequiredService<IUserSessionService>();
        await sessionService.RevokeAsync(sessionId, "EA-009 bearer session regression test");
    }

    private static async Task SetTenantStatusAsync(
        WebApplicationFactory<Program> factory,
        TestIdentity identity,
        TenantStatus status)
    {
        using var scope = factory.Services.CreateScope();
        SetTenant(scope.ServiceProvider, identity.TenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await dbContext.Tenants.IgnoreQueryFilters().SingleAsync(
            entity => entity.Id == identity.TenantId);
        tenant.Status = status;
        tenant.StatusChangedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
    }

    private static async Task ReplaceAuthorizationAsync(
        WebApplicationFactory<Program> factory,
        TestIdentity identity)
    {
        using var scope = factory.Services.CreateScope();
        SetTenant(scope.ServiceProvider, identity.TenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await dbContext.Roles.SingleAsync(entity => entity.Id == identity.RoleId);
        var existingRelation = await dbContext.RolePermissions.SingleAsync(
            entity => entity.RoleId == identity.RoleId && entity.PermissionId == identity.PermissionId);
        role.Code = identity.UpdatedRoleCode;
        dbContext.RolePermissions.Remove(existingRelation);

        var updatedPermissionId = Guid.NewGuid();
        dbContext.Permissions.Add(new Permission
        {
            Id = updatedPermissionId,
            TenantId = identity.TenantId,
            Code = identity.UpdatedPermissionCode,
            Name = "EA-009 updated integration permission",
            Group = "EA-009"
        });
        dbContext.RolePermissions.Add(new RolePermission
        {
            Id = Guid.NewGuid(),
            TenantId = identity.TenantId,
            RoleId = identity.RoleId,
            PermissionId = updatedPermissionId
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task EnableIpWhitelistAsync(
        WebApplicationFactory<Program> factory,
        TestIdentity identity)
    {
        using var scope = factory.Services.CreateScope();
        SetTenant(scope.ServiceProvider, identity.TenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var policy = await dbContext.SecurityPolicies.SingleAsync(
            entity => entity.TenantId == identity.TenantId);
        policy.EnableIpWhitelist = true;
        await dbContext.SaveChangesAsync();
    }

    private static async Task CleanupIdentityAsync(
        WebApplicationFactory<Program> factory,
        TestIdentity identity)
    {
        using var scope = factory.Services.CreateScope();
        SetTenant(scope.ServiceProvider, identity.TenantId);

        var tokenRevocationService = scope.ServiceProvider.GetRequiredService<ITokenRevocationService>();
        await tokenRevocationService.RevokeUserRefreshTokensAsync(identity.UserId);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.RolePermissions.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.UserRoles.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.UserSessions.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.LoginFailureRecords.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.LoginLogs.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.OperationLogs.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.IpAccessRules.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.SecurityPolicies.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.Permissions.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.Roles.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.Users.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == identity.TenantId)
            .ExecuteDeleteAsync();
        await dbContext.Tenants.IgnoreQueryFilters()
            .Where(entity => entity.Id == identity.TenantId)
            .ExecuteDeleteAsync();
    }

    private static void SetTenant(IServiceProvider serviceProvider, Guid tenantId)
    {
        serviceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId, "Test");
    }

    private static FormUrlEncodedContent CreatePasswordGrant(string username, string password)
    {
        return new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = "password",
            ["client_id"] = "permission-admin",
            ["client_secret"] = ClientSecret,
            ["username"] = username,
            ["password"] = password,
            ["scope"] = "permission-system-api offline_access"
        });
    }

    private static FormUrlEncodedContent CreateRefreshGrant(string refreshToken)
    {
        return new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "permission-admin",
            ["client_secret"] = ClientSecret,
            ["refresh_token"] = refreshToken,
            ["scope"] = "permission-system-api offline_access"
        });
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }
    }

    private sealed class OAuthErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    private sealed record TestIdentity(
        Guid TenantId,
        Guid UserId,
        Guid RoleId,
        Guid PermissionId,
        string TenantCode,
        string UserName,
        string Password,
        string RoleCode,
        string PermissionCode,
        string UpdatedRoleCode,
        string UpdatedPermissionCode)
    {
        public static TestIdentity Create()
        {
            var suffix = Guid.NewGuid().ToString("N");
            return new TestIdentity(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"ea009-{suffix}",
                $"ea009_{suffix}",
                "EA009_Password_123!",
                $"ea009-role-{suffix}",
                $"ea009:permission:{suffix}",
                $"ea009-role-updated-{suffix}",
                $"ea009:permission:updated:{suffix}");
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnvName)))
            {
                Skip = $"Set {ConnectionEnvName} to run SQL Server integration tests.";
            }
        }
    }

    private sealed class FixedClientIpAccessor : IClientIpAccessor
    {
        private readonly string _clientIp;

        public FixedClientIpAccessor(string clientIp)
        {
            _clientIp = clientIp;
        }

        public string GetClientIp(HttpContext context)
        {
            return _clientIp;
        }
    }
}
