using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using PermissionSystem.Api.Configuration;
using PermissionSystem.Infrastructure.SeedData;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace PermissionSystem.UnitTests.Authentication;

public sealed class SwaggerOAuthTests
{
    [Fact]
    public void Descriptor_UsesPublicPkceClientWithOnlyDelegatedGrants()
    {
        var descriptor = SwaggerOAuthClient.CreateDescriptor(Configuration("http://localhost:5264/swagger/oauth2-redirect.html"))!;
        Assert.Equal("permission-swagger", descriptor.ClientId);
        Assert.Equal(OpenIddictConstants.ClientTypes.Public, descriptor.ClientType);
        Assert.Null(descriptor.ClientSecret);
        Assert.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange, descriptor.Requirements);
        Assert.Contains(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, descriptor.Permissions);
        Assert.DoesNotContain(OpenIddictConstants.Permissions.GrantTypes.Password, descriptor.Permissions);
        Assert.DoesNotContain(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials, descriptor.Permissions);
        Assert.Single(descriptor.RedirectUris);
    }

    [Theory]
    [InlineData("http://example.com/swagger/oauth2-redirect.html")]
    [InlineData("https://*.example.com/swagger/oauth2-redirect.html")]
    [InlineData("https://example.com/swagger/oauth2-redirect.html?next=other")]
    [InlineData("https://example.com/swagger/oauth2-redirect.html#fragment")]
    [InlineData("https://example.com/other-callback")]
    public void Descriptor_RejectsUnsafeRedirects(string redirect)
    {
        Assert.Throws<InvalidOperationException>(() => SwaggerOAuthClient.CreateDescriptor(Configuration(redirect)));
    }

    [Fact]
    public void Ui_UsesPkceAndTenantWithoutExposingSeedSecret()
    {
        var options = new SwaggerUIOptions();
        SwaggerOAuthConfiguration.Configure(options, Configuration("https://localhost:7281/swagger/oauth2-redirect.html"));
        Assert.Equal("permission-swagger", options.OAuthConfigObject.ClientId);
        Assert.Null(options.OAuthConfigObject.ClientSecret);
        Assert.True(options.OAuthConfigObject.UsePkceWithAuthorizationCodeGrant);
        Assert.Equal("default", options.OAuthConfigObject.AdditionalQueryStringParams["tenant"]);
    }

    private static IConfiguration Configuration(string redirect) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Swagger:OAuthRedirectUris:0"] = redirect,
            ["Swagger:TenantCode"] = "default",
            ["SeedData:OAuthClientSecret"] = "synthetic-test-value"
        }).Build();
}
