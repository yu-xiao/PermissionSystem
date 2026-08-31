using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
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
    private readonly IAntiforgery _antiforgery;
    private readonly IDataProtector _authorizationTicketProtector;

    private const string BrowserScheme = "PermissionAuthorization";
    private const string AuthorizationTicketField = "authorization_ticket";
    private const string DecisionField = "decision";
    private const string LoginTicketPurpose = "permission-system:oauth-authorization-ticket:v1";

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
        ILogger<ConnectController> logger,
        IAntiforgery antiforgery,
        IDataProtectionProvider dataProtectionProvider)
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
        _antiforgery = antiforgery;
        _authorizationTicketProtector = dataProtectionProvider.CreateProtector(LoginTicketPurpose);
    }

    [HttpGet("authorize")]
    [HttpPost("authorize")]
    [AllowAnonymous]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict server request is not available.");

        if (!request.IsAuthorizationCodeFlow())
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.UnsupportedResponseType,
                "Only the authorization code response type is supported.");
        }

        var requestedTenant = request.GetParameter("tenant").ToString();
        var authorizationTenantId = await _userCredentialValidator.ResolveActiveTenantIdAsync(
            requestedTenant ?? string.Empty,
            cancellationToken);
        if (!authorizationTenantId.HasValue)
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidRequest,
                "A valid active tenant code is required.");
        }
        _tenantContext.SetTenant(authorizationTenantId.Value, "AuthorizationRequest");

        var ticketValue = await ReadAuthorizationTicketAsync(request, cancellationToken);
        if (ticketValue is null)
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidRequest,
                "The authorization request is invalid or has expired.");
        }

        if (HttpContext.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _antiforgery.ValidateRequestAsync(HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return ForbidWithOAuthError(
                    OpenIddictConstants.Errors.InvalidRequest,
                    "The authorization form could not be validated.");
            }
        }

        var authentication = await HttpContext.AuthenticateAsync(BrowserScheme);
        if (authentication.Succeeded && authentication.Principal is not null &&
            (!TryGetUserIdentity(authentication.Principal, out var browserIdentity) ||
             browserIdentity.TenantId != authorizationTenantId.Value))
        {
            await HttpContext.SignOutAsync(BrowserScheme);
            return await RenderLoginAsync(ticketValue, null, cancellationToken);
        }
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            if (HttpContext.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                var form = await HttpContext.Request.ReadFormAsync(cancellationToken);
                var username = form["username"].ToString();
                var password = form["password"].ToString();
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return await RenderLoginAsync(ticketValue, "请输入用户名和密码。", cancellationToken);
                }

                var user = await ValidateBrowserCredentialsAsync(username, password, cancellationToken);
                if (user is null)
                {
                    return await RenderLoginAsync(ticketValue, "用户名或密码不正确。", cancellationToken);
                }

                var identity = new ClaimsIdentity(BrowserScheme, OpenIddictConstants.Claims.Name, OpenIddictConstants.Claims.Role);
                UserTokenPrincipalFactory.AddUserStateClaims(identity, user);
                await HttpContext.SignInAsync(
                    BrowserScheme,
                    new ClaimsPrincipal(identity),
                    new AuthenticationProperties
                    {
                        IsPersistent = false,
                        AllowRefresh = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    });

                return Redirect(BuildAuthorizationUrl(ticketValue));
            }

            return await RenderLoginAsync(ticketValue, null, cancellationToken);
        }

        if (HttpContext.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            var form = await HttpContext.Request.ReadFormAsync(cancellationToken);
            var decision = form[DecisionField].ToString();
            if (string.Equals(decision, "deny", StringComparison.OrdinalIgnoreCase))
            {
                return ForbidWithOAuthError(
                    OpenIddictConstants.Errors.AccessDenied,
                    "用户拒绝了授权请求。");
            }

            if (!string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase))
            {
                return await RenderConsentAsync(ticketValue, authentication.Principal, "请选择是否授权。", cancellationToken);
            }

            var user = await ResolveBrowserUserAsync(authentication.Principal, cancellationToken);
            if (user is null)
            {
                await HttpContext.SignOutAsync(BrowserScheme);
                return Redirect(BuildAuthorizationUrl(ticketValue));
            }

            var principal = BuildAuthorizationCodePrincipal(user, request);
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return await RenderConsentAsync(ticketValue, authentication.Principal, null, cancellationToken);
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

        if (request.IsAuthorizationCodeGrantType())
        {
            return await HandleAuthorizationCodeGrantAsync(request, cancellationToken);
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
        await HttpContext.SignOutAsync(BrowserScheme);
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await RevokeCurrentSessionAsync("Logout.", cancellationToken);
        await HttpContext.SignOutAsync(BrowserScheme);
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

    private async Task<IActionResult> HandleAuthorizationCodeGrantAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (result.Principal is null || !TryGetUserIdentity(result.Principal, out var identity))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The authorization code is no longer valid.");
        }

        var user = await _userCredentialValidator.GetAuthenticationStateAsync(
            identity.TenantId,
            identity.UserId,
            cancellationToken);
        if (user is null || !await _tenantStatusChecker.IsActiveAsync(identity.TenantId, cancellationToken))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The user is no longer active.");
        }

        _tenantContext.SetTenant(identity.TenantId, "AuthorizationCode");
        if (!await _securityPolicyService.IsIpAllowedAsync(
            _clientIpAccessor.GetClientIp(HttpContext),
            cancellationToken))
        {
            return ForbidWithOAuthError(
                OpenIddictConstants.Errors.InvalidGrant,
                "The authorization code cannot be used from the current IP address.");
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

        var principal = UserTokenPrincipalFactory.RefreshUserState(result.Principal, user);
        var claimsIdentity = (ClaimsIdentity?)principal.Identity
            ?? throw new InvalidOperationException("The authorization principal does not contain a claims identity.");
        AddAccessTokenClaim(claimsIdentity, ClaimConstants.SessionId, session.SessionId);
        AddAccessTokenClaim(claimsIdentity, ClaimConstants.AccessTokenId, session.AccessTokenId);
        AddAccessTokenClaim(claimsIdentity, ClaimConstants.RefreshTokenId, session.RefreshTokenId);

        var scopes = result.Principal.GetScopes().ToArray();
        if (scopes.Length == 0)
        {
            scopes = request.GetScopes().ToArray();
        }
        principal.SetScopes(scopes);
        ConfigureTokenResources(principal, request);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private ClaimsPrincipal BuildAuthorizationCodePrincipal(AuthenticatedUser user, OpenIddictRequest request)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        UserTokenPrincipalFactory.AddUserStateClaims(identity, user);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        ConfigureTokenResources(principal, request);
        return principal;
    }

    private async Task<AuthenticatedUser?> ValidateBrowserCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var clientIp = _clientIpAccessor.GetClientIp(HttpContext);
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("The tenant context is not available for the authorization request.");
        try
        {
            await _securityPolicyService.EnsureLoginAllowedAsync(username, clientIp, cancellationToken);
        }
        catch (BusinessException exception)
        {
            ObservabilityMetrics.RecordLoginAttempt("rejected");
            await WriteLoginLogAsync(
                tenantId,
                null,
                username,
                "Failed",
                exception.Message,
                cancellationToken,
                "authorization_code");
            return null;
        }

        var user = await _userCredentialValidator.ValidateAsync(username, password, cancellationToken);
        if (user is null)
        {
            ObservabilityMetrics.RecordLoginAttempt("failed");
            await WriteLoginLogAsync(
                tenantId,
                null,
                username,
                "Failed",
                "The username/password couple is invalid.",
                cancellationToken,
                "authorization_code");
            await _securityPolicyService.RecordLoginFailureAsync(
                tenantId,
                username,
                clientIp,
                cancellationToken);
            return null;
        }

        await WriteLoginLogAsync(
            user.TenantId,
            user.UserId,
            user.Username,
            "Succeeded",
            null,
            cancellationToken,
            "authorization_code");
        ObservabilityMetrics.RecordLoginAttempt("succeeded");
        await _securityPolicyService.ClearLoginFailureAsync(user.TenantId, user.Username, clientIp, cancellationToken);
        _tenantContext.SetTenant(user.TenantId, "AuthorizationLogin");
        return user;
    }

    private async Task<AuthenticatedUser?> ResolveBrowserUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserIdentity(principal, out var identity))
        {
            return null;
        }

        var user = await _userCredentialValidator.GetAuthenticationStateAsync(
            identity.TenantId,
            identity.UserId,
            cancellationToken);
        var securityStamp = principal.FindFirst(ClaimConstants.SecurityStamp)?.Value;
        return user is not null &&
            Guid.TryParseExact(securityStamp, "N", out var stamp) &&
            stamp == user.SecurityStamp
                ? user
                : null;
    }

    private async Task<string?> ReadAuthorizationTicketAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        var provided = Request.Query[AuthorizationTicketField].ToString();
        if (string.IsNullOrWhiteSpace(provided) && Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            provided = form[AuthorizationTicketField].ToString();
        }

        if (string.IsNullOrWhiteSpace(provided))
        {
            return ProtectAuthorizationTicket(request);
        }

        if (!TryUnprotectAuthorizationTicket(provided, out var ticket) || !TicketMatchesRequest(ticket!, request))
        {
            return null;
        }

        return provided;
    }

    private string ProtectAuthorizationTicket(OpenIddictRequest request)
    {
        var parameters = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        AddTicketParameter(parameters, "client_id", request.ClientId);
        AddTicketParameter(parameters, "redirect_uri", request.RedirectUri);
        AddTicketParameter(parameters, "response_type", request.ResponseType);
        AddTicketParameter(parameters, "scope", request.Scope);
        AddTicketParameter(parameters, "state", request.State);
        AddTicketParameter(parameters, "code_challenge", request.CodeChallenge);
        AddTicketParameter(parameters, "code_challenge_method", request.CodeChallengeMethod);
        AddTicketParameter(parameters, "nonce", request.Nonce);
        AddTicketParameter(parameters, "prompt", request.Prompt);
        AddTicketParameter(parameters, "response_mode", request.ResponseMode);
        AddTicketParameter(parameters, "resource", request.Resources ?? []);
        AddTicketParameter(parameters, "ui_locales", request.UiLocales);
        AddTicketParameter(parameters, "login_hint", request.LoginHint);
        AddTicketParameter(parameters, "max_age", request.MaxAge?.ToString());
        AddTicketParameter(parameters, "tenant", request.GetParameter("tenant").ToString());

        var ticket = new AuthorizationTicket
        {
            Parameters = parameters,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(ticket);
        return WebEncoders.Base64UrlEncode(_authorizationTicketProtector.Protect(json));
    }

    private bool TryUnprotectAuthorizationTicket(string value, out AuthorizationTicket? ticket)
    {
        ticket = null;
        try
        {
            var bytes = _authorizationTicketProtector.Unprotect(WebEncoders.Base64UrlDecode(value));
            ticket = JsonSerializer.Deserialize<AuthorizationTicket>(bytes);
            return ticket is not null && ticket.ExpiresAt > DateTimeOffset.UtcNow &&
                ticket.Parameters is not null && ticket.Parameters.Count > 0;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return false;
        }
    }

    private static bool TicketMatchesRequest(AuthorizationTicket ticket, OpenIddictRequest request)
    {
        return MatchesTicketParameter(ticket, "client_id", request.ClientId) &&
            MatchesTicketParameter(ticket, "redirect_uri", request.RedirectUri) &&
            MatchesTicketParameter(ticket, "response_type", request.ResponseType) &&
            MatchesTicketParameter(ticket, "scope", request.Scope) &&
            MatchesTicketParameter(ticket, "state", request.State) &&
            MatchesTicketParameter(ticket, "code_challenge", request.CodeChallenge) &&
            MatchesTicketParameter(ticket, "code_challenge_method", request.CodeChallengeMethod) &&
            MatchesTicketParameter(ticket, "nonce", request.Nonce) &&
            MatchesTicketParameter(ticket, "prompt", request.Prompt) &&
            MatchesTicketParameter(ticket, "response_mode", request.ResponseMode) &&
            MatchesTicketParameters(ticket, "resource", request.Resources ?? []) &&
            MatchesTicketParameter(ticket, "ui_locales", request.UiLocales) &&
            MatchesTicketParameter(ticket, "login_hint", request.LoginHint) &&
            MatchesTicketParameter(ticket, "max_age", request.MaxAge?.ToString()) &&
            MatchesTicketParameter(ticket, "tenant", request.GetParameter("tenant").ToString());
    }

    private static bool MatchesTicketParameter(AuthorizationTicket ticket, string key, string? value)
    {
        ticket.Parameters.TryGetValue(key, out var values);
        return string.Equals(values?.FirstOrDefault(), value, StringComparison.Ordinal);
    }

    private static bool MatchesTicketParameters(
        AuthorizationTicket ticket,
        string key,
        IEnumerable<string> values)
    {
        ticket.Parameters.TryGetValue(key, out var ticketValues);
        return (ticketValues ?? []).SequenceEqual(values, StringComparer.Ordinal);
    }

    private string BuildAuthorizationUrl(string ticketValue)
    {
        if (!TryUnprotectAuthorizationTicket(ticketValue, out var ticket) || ticket is null)
        {
            return Url.ActionLink(nameof(Authorize), values: null) ?? "/connect/authorize";
        }

        var baseUrl = Url.ActionLink(nameof(Authorize), values: null) ?? "/connect/authorize";
        var pairs = ticket.Parameters.SelectMany(pair => pair.Value.Select(value =>
            new KeyValuePair<string, string?>(pair.Key, value))).ToList();
        pairs.Add(new KeyValuePair<string, string?>(AuthorizationTicketField, ticketValue));
        return QueryHelpers.AddQueryString(baseUrl, pairs);
    }

    private async Task<IActionResult> RenderLoginAsync(string ticketValue, string? error, CancellationToken cancellationToken)
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        var hidden = BuildHiddenFields(ticketValue);
        var message = string.IsNullOrWhiteSpace(error) ? string.Empty : $"<p class=\"error\">{Encode(error)}</p>";
        return await RenderHtmlAsync($"<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>登录 PermissionSystem</title><style>{AuthorizationPageCss}</style></head><body><main class=\"card\"><h1>登录 PermissionSystem</h1><p>使用企业账号继续授权。</p>{message}<form method=\"post\" action=\"/connect/authorize\">{hidden}<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{Encode(tokens.RequestToken)}\"><label>用户名<input name=\"username\" autocomplete=\"username\" required></label><label>密码<input name=\"password\" type=\"password\" autocomplete=\"current-password\" required></label><button name=\"decision\" value=\"login\" type=\"submit\">登录并继续</button></form></main></body></html>", cancellationToken);
    }

    private async Task<IActionResult> RenderConsentAsync(string ticketValue, ClaimsPrincipal principal, string? error, CancellationToken cancellationToken)
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        var displayName = principal.Identity?.Name ?? principal.FindFirst(ClaimConstants.Username)?.Value ?? "当前用户";
        var scopes = TryUnprotectAuthorizationTicket(ticketValue, out var ticket) && ticket is not null && ticket.Parameters.TryGetValue("scope", out var values)
            ? values.FirstOrDefault() ?? string.Empty
            : string.Empty;
        var scopeText = Encode(string.Join("、", scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(FormatScope)));
        var message = string.IsNullOrWhiteSpace(error) ? string.Empty : $"<p class=\"error\">{Encode(error)}</p>";
        var hidden = BuildHiddenFields(ticketValue);
        return await RenderHtmlAsync($"<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>授权 PermissionSystem</title><style>{AuthorizationPageCss}</style></head><body><main class=\"card\"><h1>授权请求</h1><p>{Encode(displayName)}，应用请求访问 PermissionSystem。</p><p class=\"scope\">权限范围：{scopeText}</p>{message}<form method=\"post\" action=\"/connect/authorize\">{hidden}<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{Encode(tokens.RequestToken)}\"><button name=\"decision\" value=\"approve\" type=\"submit\">同意并继续</button><button class=\"secondary\" name=\"decision\" value=\"deny\" type=\"submit\">拒绝</button></form></main></body></html>", cancellationToken);
    }

    private string BuildHiddenFields(string ticketValue)
    {
        if (!TryUnprotectAuthorizationTicket(ticketValue, out var ticket) || ticket is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append($"<input type=\"hidden\" name=\"{AuthorizationTicketField}\" value=\"{Encode(ticketValue)}\">");
        foreach (var parameter in ticket.Parameters)
        {
            foreach (var value in parameter.Value)
            {
                builder.Append($"<input type=\"hidden\" name=\"{Encode(parameter.Key)}\" value=\"{Encode(value)}\">");
            }
        }
        return builder.ToString();
    }

    private async Task<IActionResult> RenderHtmlAsync(string html, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/html; charset=utf-8";
        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";
        var styleHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(AuthorizationPageCss)));
        Response.Headers.ContentSecurityPolicy =
            $"default-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'; object-src 'none'; style-src 'sha256-{styleHash}'";
        await Response.WriteAsync(html, cancellationToken);
        return new EmptyResult();
    }

    private static void AddTicketParameter(IDictionary<string, string[]> parameters, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters[key] = [value];
        }
    }

    private static void AddTicketParameter(IDictionary<string, string[]> parameters, string key, IEnumerable<string> values)
    {
        var normalized = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (normalized.Length > 0)
        {
            parameters[key] = normalized;
        }
    }

    private static string Encode(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);

    private static string FormatScope(string scope) => scope switch
    {
        OpenIddictConstants.Scopes.OpenId => "身份信息",
        OpenIddictConstants.Scopes.Profile => "个人资料",
        OpenIddictConstants.Scopes.OfflineAccess => "刷新会话",
        AiCenterConstants.ApiResource => "业务工作台",
        _ => scope
    };

    private static bool TryGetUserIdentity(ClaimsPrincipal principal, out BrowserUserIdentity identity)
    {
        var userIdValue = principal.FindFirst(ClaimConstants.UserId)?.Value ?? principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        var tenantIdValue = principal.FindFirst(ClaimConstants.TenantId)?.Value;
        if (Guid.TryParse(userIdValue, out var userId) && Guid.TryParse(tenantIdValue, out var tenantId))
        {
            identity = new BrowserUserIdentity(userId, tenantId);
            return true;
        }

        identity = default;
        return false;
    }

    private sealed class AuthorizationTicket
    {
        public Dictionary<string, string[]> Parameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public DateTimeOffset ExpiresAt { get; init; }
    }

    private readonly record struct BrowserUserIdentity(Guid UserId, Guid TenantId);

    private const string AuthorizationPageCss = "body{font-family:system-ui,sans-serif;margin:0;padding:24px;background:#f4f6f8;color:#17202a}.card{max-width:420px;margin:8vh auto;padding:24px;border-radius:12px;background:#fff;box-shadow:0 8px 30px #17202a1f}h1{font-size:22px;margin:0 0 12px}p{line-height:1.6;color:#536273}.scope{padding:12px;border-radius:8px;background:#f0f4f8;color:#17202a}label{display:block;margin:14px 0 6px;font-size:14px}input{box-sizing:border-box;width:100%;margin-top:6px;padding:11px;border:1px solid #c8d0d9;border-radius:8px;font:inherit}button{width:100%;margin-top:16px;padding:12px;border:0;border-radius:8px;background:#1769aa;color:#fff;font:inherit;font-weight:600;cursor:pointer}.secondary{background:#e8edf2;color:#17202a}.error{padding:10px;border-radius:8px;background:#fff0f0;color:#b42318}";

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
        CancellationToken cancellationToken,
        string loginType = "password")
    {
        try
        {
            await _loginLogService.CreateAsync(new CreateLoginLogRequest
            {
                TenantId = tenantId,
                UserId = userId,
                UserName = userName,
                LoginType = loginType,
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
