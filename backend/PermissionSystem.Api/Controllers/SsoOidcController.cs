using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Sso;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/sso/oidc")]
public sealed class SsoOidcController : ApiControllerBase
{
    private const string ApiResource = "permission-system-api";

    private readonly IOidcClientService _oidcClientService;
    private readonly ISsoLoginService _ssoLoginService;
    private readonly IUserSessionService _userSessionService;
    private readonly IClientIpAccessor _clientIpAccessor;
    private readonly ITenantStatusChecker _tenantStatusChecker;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SsoOidcController> _logger;

    public SsoOidcController(
        IOidcClientService oidcClientService,
        ISsoLoginService ssoLoginService,
        IUserSessionService userSessionService,
        IClientIpAccessor clientIpAccessor,
        ITenantStatusChecker tenantStatusChecker,
        IConfiguration configuration,
        ILogger<SsoOidcController> logger)
    {
        _oidcClientService = oidcClientService;
        _ssoLoginService = ssoLoginService;
        _userSessionService = userSessionService;
        _clientIpAccessor = clientIpAccessor;
        _tenantStatusChecker = tenantStatusChecker;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("{providerCode}/challenge")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult<OidcChallengeResponse>>> ChallengeAsync(
        string providerCode,
        [FromQuery] string? returnUrl,
        CancellationToken cancellationToken)
    {
        return Success(await _oidcClientService.BuildChallengeAsync(new OidcChallengeRequest
        {
            ProviderCode = providerCode,
            CallbackUrl = BuildCallbackUrl(providerCode),
            ReturnUrl = returnUrl
        }, cancellationToken));
    }

    [HttpGet("{providerCode}/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> CallbackAsync(
        string providerCode,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery(Name = "error")] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            await _ssoLoginService.RecordFailureAsync(
                null,
                null,
                string.IsNullOrWhiteSpace(errorDescription) ? error : $"{error}: {errorDescription}",
                BuildLoginContext(),
                cancellationToken);
            return Redirect(BuildFrontendCallbackUrl(null, error));
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            await _ssoLoginService.RecordFailureAsync(
                null,
                null,
                "OIDC callback code or state is missing.",
                BuildLoginContext(),
                cancellationToken);
            return Redirect(BuildFrontendCallbackUrl(null, "invalid_callback"));
        }

        try
        {
            var callbackResult = await _oidcClientService.HandleCallbackAsync(new OidcCallbackRequest
            {
                ProviderCode = providerCode,
                Code = code,
                State = state,
                CallbackUrl = BuildCallbackUrl(providerCode)
            }, cancellationToken);
            var loginCode = await _ssoLoginService.CompleteLoginAsync(
                callbackResult.Provider,
                callbackResult.ExternalUser,
                BuildLoginContext(),
                cancellationToken);

            return Redirect(BuildFrontendCallbackUrl(loginCode.LoginCode, null, callbackResult.ReturnUrl));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "OIDC SSO callback failed.");
            return Redirect(BuildFrontendCallbackUrl(null, "sso_login_failed"));
        }
    }

    [HttpPost("exchange")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> ExchangeAsync(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict server request is not available.");
        if (!string.Equals(request.GrantType, SsoGrantTypes.OidcLoginCode, StringComparison.OrdinalIgnoreCase))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.UnsupportedGrantType,
                "The specified grant type is not supported by this token endpoint.");
        }

        var loginCode = request.GetParameter("login_code").ToString() ?? string.Empty;
        var user = await _ssoLoginService.ConsumeLoginCodeAsync(loginCode, cancellationToken);
        if (user is null)
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The SSO login code is invalid or expired.");
        }

        if (!await _tenantStatusChecker.IsActiveAsync(user.TenantId, cancellationToken))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The tenant is no longer active.");
        }

        var session = await _userSessionService.CreateAsync(new CreateUserSessionRequest
        {
            TenantId = user.TenantId,
            UserId = user.UserId,
            UserName = user.Username,
            IpAddress = _clientIpAccessor.GetClientIp(HttpContext),
            UserAgent = Request.Headers.UserAgent.ToString(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_configuration.GetValue("OpenIddict:RefreshTokenDays", 14))
        }, cancellationToken);

        var principal = BuildPrincipal(user, session, request);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private ClaimsPrincipal BuildPrincipal(
        AuthenticatedUser user,
        CreatedUserSessionResponse session,
        OpenIddictRequest request)
    {
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
        var scopes = request.GetScopes().ToArray();
        principal.SetScopes(scopes.Length > 0
            ? scopes
            : [OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.OfflineAccess, ApiResource]);
        principal.SetResources(ApiResource);
        return principal;
    }

    private string BuildCallbackUrl(string providerCode)
    {
        return Url.ActionLink(
            nameof(CallbackAsync),
            values: new { providerCode }) ?? $"{Request.Scheme}://{Request.Host}/api/sso/oidc/{Uri.EscapeDataString(providerCode)}/callback";
    }

    private string BuildFrontendCallbackUrl(string? loginCode, string? error, string? returnUrl = null)
    {
        var path = string.IsNullOrWhiteSpace(returnUrl) ? "/sso/callback" : returnUrl;
        var separator = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        if (!string.IsNullOrWhiteSpace(loginCode))
        {
            return $"{path}{separator}login_code={Uri.EscapeDataString(loginCode)}";
        }

        return $"{path}{separator}error={Uri.EscapeDataString(error ?? "sso_login_failed")}";
    }

    private SsoLoginContext BuildLoginContext()
    {
        return new SsoLoginContext
        {
            IpAddress = _clientIpAccessor.GetClientIp(HttpContext),
            UserAgent = Request.Headers.UserAgent.ToString(),
            TraceId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier
        };
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
