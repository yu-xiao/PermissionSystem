using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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

    private static WebApplicationFactory<Program> CreateFactory(string connectionString)
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
                        ["SeedData:AdminPassword"] = AdminPassword,
                        ["SeedData:OAuthClientSecret"] = ClientSecret,
                        ["Security:SystemConfigEncryptionKey"] = "0123456789abcdef0123456789abcdef",
                        ["Cors:AllowedOrigins:0"] = "http://localhost"
                    });
                });
            });
    }

    private static async Task<TokenResponse> RequestPasswordTokenAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsync("/connect/token", CreatePasswordGrant(username, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static async Task<TokenResponse> RequestRefreshTokenAsync(HttpClient client, string refreshToken)
    {
        var response = await client.PostAsync("/connect/token", CreateRefreshGrant(refreshToken));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
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
}
