using PermissionSystem.Application.Authentication;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Application.Sso;

public static class SsoGrantTypes
{
    public const string OidcLoginCode = "sso_oidc";
}

public sealed class ExternalSsoUser
{
    public string ExternalUserId { get; init; } = string.Empty;

    public string? UserName { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? DisplayName { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public IReadOnlyCollection<string> Departments { get; init; } = [];

    public IReadOnlyDictionary<string, string> Claims { get; init; } = new Dictionary<string, string>();
}

public sealed class SsoLoginContext
{
    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? TraceId { get; init; }
}

public sealed class SsoLoginCodeResponse
{
    public string LoginCode { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }

    public AuthenticatedUser User { get; init; } = new(
        Guid.Empty,
        string.Empty,
        Guid.Empty,
        null,
        Guid.Empty,
        [],
        []);
}

public sealed class SsoLoginCodeCacheEntry
{
    public Guid UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public Guid TenantId { get; init; }

    public Guid? DepartmentId { get; init; }

    public Guid SecurityStamp { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public IReadOnlyCollection<string> PermissionCodes { get; init; } = [];
}

public sealed class OidcChallengeRequest
{
    public string ProviderCode { get; init; } = string.Empty;

    public string CallbackUrl { get; init; } = string.Empty;

    public string? ReturnUrl { get; init; }
}

public sealed class OidcChallengeResponse
{
    public string RedirectUrl { get; init; } = string.Empty;
}

public sealed class OidcCallbackRequest
{
    public string ProviderCode { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string CallbackUrl { get; init; } = string.Empty;
}

public sealed class OidcCallbackResult
{
    public SsoProvider Provider { get; init; } = new();

    public ExternalSsoUser ExternalUser { get; init; } = new();

    public string? ReturnUrl { get; init; }
}

public interface ISsoLoginService
{
    Task<SsoLoginCodeResponse> CompleteLoginAsync(
        SsoProvider provider,
        ExternalSsoUser externalUser,
        SsoLoginContext context,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> ConsumeLoginCodeAsync(string loginCode, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(
        SsoProvider? provider,
        ExternalSsoUser? externalUser,
        string failureReason,
        SsoLoginContext context,
        CancellationToken cancellationToken = default);
}

public interface IOidcClientService
{
    Task<OidcChallengeResponse> BuildChallengeAsync(
        OidcChallengeRequest request,
        CancellationToken cancellationToken = default);

    Task<OidcCallbackResult> HandleCallbackAsync(
        OidcCallbackRequest request,
        CancellationToken cancellationToken = default);
}
