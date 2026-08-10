using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Sso;

public sealed class SsoProviderQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public SsoProviderType? ProviderType { get; init; }

    public bool? Enabled { get; init; }
}

public sealed class SsoProviderListResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string ProviderCode { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public SsoProviderType ProviderType { get; init; }

    public bool Enabled { get; init; }

    public string? Authority { get; init; }

    public string? MetadataAddress { get; init; }

    public string? Scopes { get; init; }

    public string CallbackPath { get; init; } = string.Empty;

    public bool UsePkce { get; init; }

    public bool AutoCreateUser { get; init; }

    public bool AutoBindUser { get; init; }

    public bool AllowLocalLoginFallback { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class SsoProviderDetailResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string ProviderCode { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public SsoProviderType ProviderType { get; init; }

    public bool Enabled { get; init; }

    public string? Authority { get; init; }

    public string? MetadataAddress { get; init; }

    public string? ClientId { get; init; }

    public string ClientSecret { get; init; } = string.Empty;

    public bool HasClientSecret { get; init; }

    public string Scopes { get; init; } = string.Empty;

    public string CallbackPath { get; init; } = string.Empty;

    public string ResponseType { get; init; } = string.Empty;

    public bool UsePkce { get; init; }

    public bool GetClaimsFromUserInfoEndpoint { get; init; }

    public string UserIdClaim { get; init; } = string.Empty;

    public string UserNameClaim { get; init; } = string.Empty;

    public string EmailClaim { get; init; } = string.Empty;

    public string PhoneClaim { get; init; } = string.Empty;

    public string DisplayNameClaim { get; init; } = string.Empty;

    public string RoleClaim { get; init; } = string.Empty;

    public string DepartmentClaim { get; init; } = string.Empty;

    public bool AutoCreateUser { get; init; }

    public bool AutoBindUser { get; init; }

    public string? DefaultRoleIds { get; init; }

    public bool AllowLocalLoginFallback { get; init; }

    public string? LogoutRedirectUri { get; init; }

    public string? Remark { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class CreateSsoProviderRequest
{
    public Guid TenantId { get; init; }

    public string ProviderCode { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public SsoProviderType ProviderType { get; init; } = SsoProviderType.Oidc;

    public bool Enabled { get; init; } = true;

    public string? Authority { get; init; }

    public string? MetadataAddress { get; init; }

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public string? Scopes { get; init; }

    public string? CallbackPath { get; init; }

    public string? ResponseType { get; init; }

    public bool UsePkce { get; init; } = true;

    public bool GetClaimsFromUserInfoEndpoint { get; init; } = true;

    public string? UserIdClaim { get; init; }

    public string? UserNameClaim { get; init; }

    public string? EmailClaim { get; init; }

    public string? PhoneClaim { get; init; }

    public string? DisplayNameClaim { get; init; }

    public string? RoleClaim { get; init; }

    public string? DepartmentClaim { get; init; }

    public bool AutoCreateUser { get; init; }

    public bool AutoBindUser { get; init; } = true;

    public string? DefaultRoleIds { get; init; }

    public bool AllowLocalLoginFallback { get; init; } = true;

    public string? LogoutRedirectUri { get; init; }

    public string? Remark { get; init; }
}

public sealed class UpdateSsoProviderRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public SsoProviderType ProviderType { get; init; } = SsoProviderType.Oidc;

    public string? Authority { get; init; }

    public string? MetadataAddress { get; init; }

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public string? Scopes { get; init; }

    public string? CallbackPath { get; init; }

    public string? ResponseType { get; init; }

    public bool UsePkce { get; init; } = true;

    public bool GetClaimsFromUserInfoEndpoint { get; init; } = true;

    public string? UserIdClaim { get; init; }

    public string? UserNameClaim { get; init; }

    public string? EmailClaim { get; init; }

    public string? PhoneClaim { get; init; }

    public string? DisplayNameClaim { get; init; }

    public string? RoleClaim { get; init; }

    public string? DepartmentClaim { get; init; }

    public bool AutoCreateUser { get; init; }

    public bool AutoBindUser { get; init; } = true;

    public string? DefaultRoleIds { get; init; }

    public bool AllowLocalLoginFallback { get; init; } = true;

    public string? LogoutRedirectUri { get; init; }

    public string? Remark { get; init; }
}

public sealed class TestSsoProviderRequest
{
    public string? Authority { get; init; }

    public string? MetadataAddress { get; init; }

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }
}

public sealed class SsoProviderTestResponse
{
    public bool Succeeded { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? MetadataAddress { get; init; }
}

public interface ISsoProviderService
{
    Task<PagedResult<SsoProviderListResponse>> GetPagedAsync(
        SsoProviderQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SsoProviderListResponse>> GetEnabledAsync(CancellationToken cancellationToken = default);

    Task<SsoProviderDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SsoProviderDetailResponse> CreateAsync(
        CreateSsoProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<SsoProviderDetailResponse> UpdateAsync(
        Guid id,
        UpdateSsoProviderRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);

    Task<SsoProviderTestResponse> TestAsync(
        Guid id,
        TestSsoProviderRequest request,
        CancellationToken cancellationToken = default);
}
