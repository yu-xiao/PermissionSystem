using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Sso;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using System.IdentityModel.Tokens.Jwt;

namespace PermissionSystem.Infrastructure.Sso;

public sealed class OidcClientService : IOidcClientService
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<SsoProvider> _providerRepository;
    private readonly IConfigValueProtector _valueProtector;
    private readonly ICacheService _cacheService;
    private readonly ISsoConfiguration _ssoConfiguration;

    public OidcClientService(
        IRepository<SsoProvider> providerRepository,
        IConfigValueProtector valueProtector,
        ICacheService cacheService,
        ISsoConfiguration? ssoConfiguration = null)
    {
        _providerRepository = providerRepository;
        _valueProtector = valueProtector;
        _cacheService = cacheService;
        _ssoConfiguration = ssoConfiguration ?? new DefaultSsoConfiguration();
    }

    public async Task<OidcChallengeResponse> BuildChallengeAsync(
        OidcChallengeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureOidcEnabled();
        var provider = GetEnabledOidcProvider(request.ProviderCode);
        var configuration = await GetConfigurationAsync(provider, cancellationToken);
        var state = GenerateUrlSafeRandom(32);
        var nonce = GenerateUrlSafeRandom(32);
        var codeVerifier = provider.UsePkce ? GenerateUrlSafeRandom(64) : string.Empty;
        var codeChallenge = provider.UsePkce ? CreateCodeChallenge(codeVerifier) : null;

        await _cacheService.SetAsync(
            BuildStateCacheKey(state),
            new OidcStateCacheEntry
            {
                ProviderId = provider.Id,
                ProviderCode = provider.ProviderCode,
                TenantId = provider.TenantId,
                Nonce = nonce,
                CodeVerifier = codeVerifier,
                ReturnUrl = NormalizeReturnUrl(request.ReturnUrl)
            },
            StateTtl,
            cancellationToken: cancellationToken);

        var redirectUrl = BuildAuthorizeUrl(
            configuration.AuthorizationEndpoint,
            provider,
            request.CallbackUrl,
            state,
            nonce,
            codeChallenge);

        return new OidcChallengeResponse
        {
            RedirectUrl = redirectUrl
        };
    }

    public async Task<OidcCallbackResult> HandleCallbackAsync(
        OidcCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureOidcEnabled();
        var state = await ConsumeStateAsync(request.State, cancellationToken)
            ?? throw new BusinessException(ErrorCode.Forbidden, "OIDC state is invalid or expired.");
        if (!string.Equals(state.ProviderCode, request.ProviderCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ErrorCode.Forbidden, "OIDC state does not match provider.");
        }

        var provider = _providerRepository.QueryForTenant(state.TenantId)
            .FirstOrDefault(entity =>
                !entity.IsDeleted &&
                entity.Id == state.ProviderId &&
                entity.TenantId == state.TenantId &&
                entity.ProviderType == SsoProviderType.Oidc &&
                entity.Enabled)
            ?? throw new BusinessException(ErrorCode.NotFound, "OIDC provider was not found.");
        var configuration = await GetConfigurationAsync(provider, cancellationToken);
        var tokenResponse = await RedeemAuthorizationCodeAsync(
            provider,
            configuration,
            request.Code,
            request.CallbackUrl,
            state.CodeVerifier,
            cancellationToken);
        var principal = ValidateIdToken(provider, configuration, tokenResponse.IdToken, state.Nonce);
        var userInfo = provider.GetClaimsFromUserInfoEndpoint && !string.IsNullOrWhiteSpace(configuration.UserInfoEndpoint)
            ? await GetUserInfoAsync(configuration.UserInfoEndpoint, tokenResponse.AccessToken, cancellationToken)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new OidcCallbackResult
        {
            Provider = provider,
            ExternalUser = BuildExternalUser(provider, principal, userInfo),
            ReturnUrl = state.ReturnUrl
        };
    }

    private SsoProvider GetEnabledOidcProvider(string providerCode)
    {
        EnsureOidcEnabled();
        var normalizedCode = NormalizeProviderCode(providerCode);
        return _providerRepository.Query()
            .FirstOrDefault(entity =>
                entity.ProviderCode == normalizedCode &&
                entity.ProviderType == SsoProviderType.Oidc &&
                entity.Enabled)
            ?? throw new BusinessException(ErrorCode.NotFound, "OIDC provider was not found.");
    }

    private async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        SsoProvider provider,
        CancellationToken cancellationToken)
    {
        var metadataAddress = ResolveMetadataAddress(provider);
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever());
        return await manager.GetConfigurationAsync(cancellationToken);
    }

    private async Task<OidcTokenResponse> RedeemAuthorizationCodeAsync(
        SsoProvider provider,
        OpenIdConnectConfiguration configuration,
        string code,
        string callbackUrl,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.TokenEndpoint))
        {
            throw new BusinessException(ErrorCode.BusinessError, "OIDC token endpoint is missing.");
        }

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = callbackUrl,
            ["client_id"] = provider.ClientId ?? string.Empty
        };
        if (provider.UsePkce && !string.IsNullOrWhiteSpace(codeVerifier))
        {
            form["code_verifier"] = codeVerifier;
        }

        var clientSecret = UnprotectSecret(provider.ClientSecretEncrypted);
        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            form["client_secret"] = clientSecret;
        }

        using var response = await httpClient.PostAsync(
            configuration.TokenEndpoint,
            new FormUrlEncodedContent(form),
            cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessException(ErrorCode.BusinessError, "OIDC token exchange failed.");
        }

        var tokenResponse = JsonSerializer.Deserialize<OidcTokenResponse>(content, JsonOptions)
            ?? throw new BusinessException(ErrorCode.BusinessError, "OIDC token response is invalid.");
        if (string.IsNullOrWhiteSpace(tokenResponse.IdToken))
        {
            throw new BusinessException(ErrorCode.BusinessError, "OIDC id_token is missing.");
        }

        return tokenResponse;
    }

    private ClaimsPrincipal ValidateIdToken(
        SsoProvider provider,
        OpenIdConnectConfiguration configuration,
        string idToken,
        string nonce)
    {
        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
        var principal = handler.ValidateToken(idToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration.Issuer,
            ValidateAudience = true,
            ValidAudience = provider.ClientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        }, out _);

        var tokenNonce = principal.FindFirst("nonce")?.Value;
        if (string.IsNullOrWhiteSpace(tokenNonce) ||
            !string.Equals(tokenNonce, nonce, StringComparison.Ordinal))
        {
            throw new BusinessException(ErrorCode.Forbidden, "OIDC nonce is invalid.");
        }

        return principal;
    }

    private async Task<IReadOnlyDictionary<string, string>> GetUserInfoAsync(
        string userInfoEndpoint,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return FlattenJsonObject(content);
    }

    private ExternalSsoUser BuildExternalUser(
        SsoProvider provider,
        ClaimsPrincipal principal,
        IReadOnlyDictionary<string, string> userInfo)
    {
        var claims = principal.Claims
            .GroupBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => string.Join(",", group.Select(claim => claim.Value)),
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in userInfo)
        {
            claims[item.Key] = item.Value;
        }

        var externalUserId = GetClaimValue(claims, provider.UserIdClaim);
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "OIDC external user id claim is missing.");
        }

        return new ExternalSsoUser
        {
            ExternalUserId = externalUserId,
            UserName = GetClaimValue(claims, provider.UserNameClaim),
            Email = GetClaimValue(claims, provider.EmailClaim),
            Phone = GetClaimValue(claims, provider.PhoneClaim),
            DisplayName = GetClaimValue(claims, provider.DisplayNameClaim),
            Roles = SplitClaimValues(GetClaimValue(claims, provider.RoleClaim)),
            Departments = SplitClaimValues(GetClaimValue(claims, provider.DepartmentClaim)),
            Claims = claims
        };
    }

    private async Task<OidcStateCacheEntry?> ConsumeStateAsync(
        string state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var cacheKey = BuildStateCacheKey(state.Trim());
        var entry = await _cacheService.GetAsync<OidcStateCacheEntry>(cacheKey, cancellationToken);
        await _cacheService.RemoveAsync(cacheKey, cancellationToken);
        return entry;
    }

    private static string BuildAuthorizeUrl(
        string authorizationEndpoint,
        SsoProvider provider,
        string callbackUrl,
        string state,
        string nonce,
        string? codeChallenge)
    {
        if (string.IsNullOrWhiteSpace(authorizationEndpoint))
        {
            throw new BusinessException(ErrorCode.BusinessError, "OIDC authorization endpoint is missing.");
        }

        var query = new Dictionary<string, string?>
        {
            ["response_type"] = provider.ResponseType,
            ["client_id"] = provider.ClientId,
            ["redirect_uri"] = callbackUrl,
            ["scope"] = provider.Scopes,
            ["state"] = state,
            ["nonce"] = nonce
        };
        if (provider.UsePkce && !string.IsNullOrWhiteSpace(codeChallenge))
        {
            query["code_challenge"] = codeChallenge;
            query["code_challenge_method"] = "S256";
        }

        return authorizationEndpoint + "?" + string.Join(
            "&",
            query
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}"));
    }

    private static IReadOnlyDictionary<string, string> FlattenJsonObject(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Array => string.Join(",", property.Value.EnumerateArray().Select(ToStringValue)),
                JsonValueKind.Object => property.Value.GetRawText(),
                _ => ToStringValue(property.Value)
            };
        }

        return result;
    }

    private static string ToStringValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
    }

    private string? UnprotectSecret(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : _valueProtector.Unprotect(value);
    }

    private string ResolveMetadataAddress(SsoProvider provider)
    {
        string metadataAddress;
        if (!string.IsNullOrWhiteSpace(provider.MetadataAddress))
        {
            metadataAddress = provider.MetadataAddress.Trim();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(provider.Authority))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "OIDC authority is required.");
            }

            metadataAddress = provider.Authority.Trim().TrimEnd('/') + "/.well-known/openid-configuration";
        }

        if (_ssoConfiguration.RequireHttpsMetadata &&
            (!Uri.TryCreate(metadataAddress, UriKind.Absolute, out var uri) ||
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "HTTPS metadata is required.");
        }

        return metadataAddress;
    }

    private void EnsureOidcEnabled()
    {
        if (!_ssoConfiguration.Enabled)
        {
            throw new BusinessException(ErrorCode.Forbidden, "SSO is disabled globally.");
        }

        if (!_ssoConfiguration.EnableOidc)
        {
            throw new BusinessException(ErrorCode.Forbidden, "OIDC SSO is disabled globally.");
        }
    }

    private static string NormalizeProviderCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Provider code is required.");
        }

        return value.Trim().ToUpperInvariant();
    }

    internal static string? NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        var value = returnUrl.Trim();
        if (!value.StartsWith("/", StringComparison.Ordinal) ||
            value.StartsWith("//", StringComparison.Ordinal) ||
            value.Contains('\\') ||
            value.Any(char.IsControl))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Return URL must be a local path.");
        }

        return value;
    }

    private static string? GetClaimValue(IReadOnlyDictionary<string, string> claims, string claimName)
    {
        return claims.TryGetValue(claimName, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static IReadOnlyCollection<string> SplitClaimValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '|', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GenerateUrlSafeRandom(int byteCount)
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string BuildStateCacheKey(string state)
    {
        return $"ps:sso:oidc:state:{state}";
    }
}

public sealed class OidcStateCacheEntry
{
    public Guid ProviderId { get; init; }

    public string ProviderCode { get; init; } = string.Empty;

    public Guid TenantId { get; init; }

    public string Nonce { get; init; } = string.Empty;

    public string CodeVerifier { get; init; } = string.Empty;

    public string? ReturnUrl { get; init; }
}

public sealed class OidcTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("id_token")]
    public string IdToken { get; init; } = string.Empty;
}
