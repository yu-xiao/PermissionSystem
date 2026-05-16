using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Controllers;

[ApiController]
[Route("connect")]
public sealed class ConnectController : ControllerBase
{
    private const string ApiResource = "permission-system-api";

    private readonly IUserCredentialValidator _userCredentialValidator;

    public ConnectController(IUserCredentialValidator userCredentialValidator)
    {
        _userCredentialValidator = userCredentialValidator;
    }

    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict server request is not available.");

        if (request.IsPasswordGrantType())
        {
            return await HandlePasswordGrantAsync(request, cancellationToken);
        }

        if (request.IsRefreshTokenGrantType())
        {
            return await HandleRefreshTokenGrantAsync();
        }

        if (request.IsClientCredentialsGrantType())
        {
            return HandleClientCredentialsGrant(request);
        }

        return ForbidWithOAuthError(
            OpenIddictConstants.Errors.UnsupportedGrantType,
            "The specified grant type is not supported by this authorization server.");
    }

    [HttpPost("revoke")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Revoke()
    {
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandlePasswordGrantAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userCredentialValidator.ValidateAsync(
            request.Username ?? string.Empty,
            request.Password ?? string.Empty,
            cancellationToken);

        if (user is null)
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The username/password couple is invalid.");
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Subject, user.UserId.ToString());
        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Name, user.Username);
        AddAccessTokenClaim(identity, ClaimConstants.UserId, user.UserId.ToString());
        AddAccessTokenClaim(identity, ClaimConstants.Username, user.Username);
        AddAccessTokenClaim(identity, ClaimConstants.TenantId, user.TenantId.ToString());

        foreach (var role in user.Roles)
        {
            AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Role, role);
        }

        foreach (var permissionCode in user.PermissionCodes)
        {
            AddAccessTokenClaim(identity, ClaimConstants.PermissionCode, permissionCode);
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(ApiResource);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshTokenGrantAsync()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (result.Principal is null)
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The refresh token is no longer valid.");
        }

        return SignIn(result.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private IActionResult HandleClientCredentialsGrant(OpenIddictRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidClient,
                "The client identifier is missing.");
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Subject, request.ClientId);
        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.ClientId, request.ClientId);
        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Name, request.ClientId);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(ApiResource);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private IActionResult ForbidWithOAuthError(string error, string description)
    {
        var properties = new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        });

        return Forbid(properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static void AddAccessTokenClaim(ClaimsIdentity identity, string type, string value)
    {
        identity.AddClaim(new Claim(type, value).SetDestinations(OpenIddictConstants.Destinations.AccessToken));
    }
}
