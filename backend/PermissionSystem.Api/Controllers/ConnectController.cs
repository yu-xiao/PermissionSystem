using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using PermissionSystem.Api.Authentication;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.LoginLogs;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Application.Security;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Api.Controllers;

[ApiController]
[Route("connect")]
public sealed class ConnectController : ControllerBase
{
    private readonly IUserCredentialValidator _userCredentialValidator;
    private readonly ILoginLogService _loginLogService;
    private readonly IUserSessionService _userSessionService;
    private readonly IUserSessionStatusChecker _userSessionStatusChecker;
    private readonly ISecurityPolicyService _securityPolicyService;
    private readonly ITenantStatusChecker _tenantStatusChecker;
    private readonly ITenantContext _tenantContext;
    private readonly ITraceContextAccessor _traceContextAccessor;
    private readonly IClientIpAccessor _clientIpAccessor;
    private readonly IMcpClientAccessService _mcpClientAccessService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConnectController> _logger;

    public ConnectController(
        IUserCredentialValidator userCredentialValidator,
        ILoginLogService loginLogService,
        IUserSessionService userSessionService,
        IUserSessionStatusChecker userSessionStatusChecker,
        ISecurityPolicyService securityPolicyService,
        ITenantStatusChecker tenantStatusChecker,
        ITenantContext tenantContext,
        ITraceContextAccessor traceContextAccessor,
        IClientIpAccessor clientIpAccessor,
        IMcpClientAccessService mcpClientAccessService,
        IConfiguration configuration,
        ILogger<ConnectController> logger)
    {
        _userCredentialValidator = userCredentialValidator;
        _loginLogService = loginLogService;
        _userSessionService = userSessionService;
        _userSessionStatusChecker = userSessionStatusChecker;
        _securityPolicyService = securityPolicyService;
        _tenantStatusChecker = tenantStatusChecker;
        _tenantContext = tenantContext;
        _traceContextAccessor = traceContextAccessor;
        _clientIpAccessor = clientIpAccessor;
        _mcpClientAccessService = mcpClientAccessService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict server request is not available.");

        if (RequestsMcpAccess(request) && request.HasScope(OpenIddictConstants.Scopes.OfflineAccess))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidScope,
                "MCP delegated access tokens cannot request offline access.");
        }

        if (request.IsPasswordGrantType())
        {
            return await HandlePasswordGrantAsync(request, cancellationToken);
        }

        if (request.IsRefreshTokenGrantType())
        {
            return await HandleRefreshTokenGrantAsync(cancellationToken);
        }

        if (request.IsClientCredentialsGrantType())
        {
            return await HandleClientCredentialsGrantAsync(request, cancellationToken);
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
        var userName = request.Username ?? string.Empty;
        var clientIp = _clientIpAccessor.GetClientIp(HttpContext);
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("The tenant context is not available for the token request.");
        try
        {
            await _securityPolicyService.EnsureLoginAllowedAsync(userName, clientIp, cancellationToken);
        }
        catch (BusinessException exception)
        {
            ObservabilityMetrics.RecordLoginAttempt("rejected");
            await WriteLoginLogAsync(
                tenantId,
                null,
                userName,
                "Failed",
                exception.Message,
                cancellationToken);

            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                exception.Message);
        }

        var user = await _userCredentialValidator.ValidateAsync(
            userName,
            request.Password ?? string.Empty,
            cancellationToken);

        if (user is null)
        {
            ObservabilityMetrics.RecordLoginAttempt("failed");
            await WriteLoginLogAsync(
                tenantId,
                null,
                userName,
                "Failed",
                "The username/password couple is invalid.",
                cancellationToken);
            await _securityPolicyService.RecordLoginFailureAsync(
                tenantId,
                userName,
                clientIp,
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
        ObservabilityMetrics.RecordLoginAttempt("succeeded");
        await _securityPolicyService.ClearLoginFailureAsync(user.TenantId, user.Username, clientIp, cancellationToken);

        var session = await _userSessionService.CreateAsync(new CreateUserSessionRequest
        {
            TenantId = user.TenantId,
            UserId = user.UserId,
            UserName = user.Username,
            IpAddress = clientIp,
            UserAgent = Request.Headers.UserAgent.ToString(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_configuration.GetValue("OpenIddict:RefreshTokenDays", 14))
        }, cancellationToken);

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        UserTokenPrincipalFactory.AddUserStateClaims(identity, user);
        AddAccessTokenClaim(identity, ClaimConstants.SessionId, session.SessionId);
        AddAccessTokenClaim(identity, ClaimConstants.AccessTokenId, session.AccessTokenId);
        AddAccessTokenClaim(identity, ClaimConstants.RefreshTokenId, session.RefreshTokenId);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        ConfigureTokenResources(principal, request);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshTokenGrantAsync(CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (result.Principal is null || !TryGetRefreshTokenIdentity(result.Principal, out var refreshIdentity))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The refresh token is no longer valid.");
        }

        _tenantContext.SetTenant(refreshIdentity.TenantId, "RefreshToken");

        if (!await _tenantStatusChecker.IsActiveAsync(refreshIdentity.TenantId, cancellationToken))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The tenant is no longer active.");
        }

        if (!await _securityPolicyService.IsIpAllowedAsync(
                _clientIpAccessor.GetClientIp(HttpContext),
                cancellationToken))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The refresh token cannot be used from the current IP address.");
        }

        if (!await _userSessionStatusChecker.IsValidForRefreshAsync(
                refreshIdentity.TenantId,
                refreshIdentity.UserId,
                refreshIdentity.SessionId,
                cancellationToken))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The session is no longer valid.");
        }

        var user = await _userCredentialValidator.GetAuthenticationStateAsync(
            refreshIdentity.TenantId,
            refreshIdentity.UserId,
            cancellationToken);
        if (user is null)
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The user is no longer active.");
        }

        var principal = UserTokenPrincipalFactory.RefreshUserState(result.Principal, user);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleClientCredentialsGrantAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
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

        if (RequestsMcpAccess(request))
        {
            var admission = await _mcpClientAccessService.ValidateTokenRequestAsync(
                request.ClientId,
                _clientIpAccessor.GetClientIp(HttpContext),
                cancellationToken);
            if (!admission.Succeeded || admission.Client is null)
            {
                return ForbidWithOAuthError(
                    OpenIddictConstants.Errors.InvalidClient,
                    "The MCP client is invalid or is not allowed from the current network.");
            }

            AddAccessTokenClaim(identity, ClaimConstants.McpCallerType, McpCallerType.ServiceClient.ToString());
            AddAccessTokenClaim(identity, ClaimConstants.TenantId, admission.Client.TenantId.ToString());
            AddAccessTokenClaim(identity, ClaimConstants.McpClientBindingId, admission.Client.ClientBindingId.ToString());
            AddAccessTokenClaim(identity, ClaimConstants.ApiClientId, admission.Client.ApiClientId.ToString());
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        ConfigureTokenResources(principal, request);

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

    private static void ConfigureTokenResources(ClaimsPrincipal principal, OpenIddictRequest request)
    {
        if (RequestsMcpAccess(request))
        {
            principal.SetResources(AiCenterConstants.McpResource);
            principal.SetAccessTokenLifetime(TimeSpan.FromMinutes(5));
            return;
        }

        principal.SetResources(AiCenterConstants.ApiResource);
    }

    private static bool RequestsMcpAccess(OpenIddictRequest request)
    {
        return request.HasScope(AiCenterConstants.McpScope);
    }

    private static void AddAccessTokenClaim(ClaimsIdentity identity, string type, string value)
    {
        identity.AddClaim(new Claim(type, value).SetDestinations(OpenIddictConstants.Destinations.AccessToken));
    }

    private static bool TryGetRefreshTokenIdentity(
        ClaimsPrincipal principal,
        out RefreshTokenIdentity refreshIdentity)
    {
        var subjectValue = principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        var userIdValue = principal.FindFirst(ClaimConstants.UserId)?.Value;
        var tenantIdValue = principal.FindFirst(ClaimConstants.TenantId)?.Value;
        var sessionId = principal.FindFirst(ClaimConstants.SessionId)?.Value;

        if (!Guid.TryParse(subjectValue, out var subjectId) ||
            !Guid.TryParse(userIdValue, out var userId) ||
            subjectId != userId ||
            !Guid.TryParse(tenantIdValue, out var tenantId) ||
            string.IsNullOrWhiteSpace(sessionId))
        {
            refreshIdentity = default;
            return false;
        }

        refreshIdentity = new RefreshTokenIdentity(tenantId, userId, sessionId);
        return true;
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
                IpAddress = _clientIpAccessor.GetClientIp(HttpContext),
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

    private readonly record struct RefreshTokenIdentity(Guid TenantId, Guid UserId, string SessionId);
}
