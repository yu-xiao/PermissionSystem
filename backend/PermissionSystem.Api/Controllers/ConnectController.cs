using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using PermissionSystem.Api.RateLimiting;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.LoginLogs;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Api.Controllers;

[ApiController]
[Route("connect")]
public sealed class ConnectController : ControllerBase
{
    private const string ApiResource = "permission-system-api";

    private readonly IUserCredentialValidator _userCredentialValidator;
    private readonly ILoginLogService _loginLogService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITraceContextAccessor _traceContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConnectController> _logger;

    public ConnectController(
        IUserCredentialValidator userCredentialValidator,
        ILoginLogService loginLogService,
        IUserSessionService userSessionService,
        ITraceContextAccessor traceContextAccessor,
        IConfiguration configuration,
        ILogger<ConnectController> logger)
    {
        _userCredentialValidator = userCredentialValidator;
        _loginLogService = loginLogService;
        _userSessionService = userSessionService;
        _traceContextAccessor = traceContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    [EnableRateLimiting(RateLimitPolicyNames.Token)]
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
    public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
    {
        await RevokeCurrentSessionAsync("Logout.", cancellationToken);
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await RevokeCurrentSessionAsync("Logout.", cancellationToken);
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
            await WriteLoginLogAsync(
                Guid.Empty,
                null,
                request.Username ?? string.Empty,
                "Failed",
                "The username/password couple is invalid.",
                cancellationToken);

            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The username/password couple is invalid.");
        }

        await WriteLoginLogAsync(
            user.TenantId,
            user.UserId,
            user.Username,
            "Succeeded",
            null,
            cancellationToken);

        var session = await _userSessionService.CreateAsync(new CreateUserSessionRequest
        {
            TenantId = user.TenantId,
            UserId = user.UserId,
            UserName = user.Username,
            IpAddress = GetClientIp(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_configuration.GetValue("OpenIddict:RefreshTokenDays", 14))
        }, cancellationToken);

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Subject, user.UserId.ToString());
        AddAccessTokenClaim(identity, OpenIddictConstants.Claims.Name, user.Username);
        AddAccessTokenClaim(identity, ClaimConstants.UserId, user.UserId.ToString());
        AddAccessTokenClaim(identity, ClaimConstants.Username, user.Username);
        AddAccessTokenClaim(identity, ClaimConstants.TenantId, user.TenantId.ToString());
        AddAccessTokenClaim(identity, ClaimConstants.SessionId, session.SessionId);
        AddAccessTokenClaim(identity, ClaimConstants.AccessTokenId, session.AccessTokenId);
        AddAccessTokenClaim(identity, ClaimConstants.RefreshTokenId, session.RefreshTokenId);
        if (user.DepartmentId.HasValue)
        {
            AddAccessTokenClaim(identity, ClaimConstants.DepartmentId, user.DepartmentId.Value.ToString());
        }

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

        var sessionId = result.Principal.FindFirst(ClaimConstants.SessionId)?.Value;
        if (!string.IsNullOrWhiteSpace(sessionId) &&
            await _userSessionService.IsRevokedAsync(sessionId))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The session is no longer valid.");
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

    private async Task RevokeCurrentSessionAsync(string reason, CancellationToken cancellationToken)
    {
        var sessionId = User.FindFirst(ClaimConstants.SessionId)?.Value;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        await _userSessionService.RevokeAsync(sessionId, reason, cancellationToken);
    }

    private async Task WriteLoginLogAsync(
        Guid tenantId,
        Guid? userId,
        string userName,
        string loginResult,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        try
        {
            await _loginLogService.CreateAsync(new CreateLoginLogRequest
            {
                TenantId = tenantId,
                UserId = userId,
                UserName = userName,
                LoginType = "password",
                IpAddress = GetClientIp(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                LoginResult = loginResult,
                FailureReason = failureReason,
                TraceId = !string.IsNullOrWhiteSpace(_traceContextAccessor.TraceId)
                    ? _traceContextAccessor.TraceId
                    : Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write login log.");
        }
    }

    private string GetClientIp()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }
}
