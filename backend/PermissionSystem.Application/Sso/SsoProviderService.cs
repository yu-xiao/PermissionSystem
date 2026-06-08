using System.Net;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Sso;

public sealed class SsoProviderService : ISsoProviderService
{
    private const string MaskedSecret = "******";
    private const string DefaultScopes = "openid profile email";
    private const string DefaultCallbackPath = "/api/sso/oidc/callback";
    private const string DefaultResponseType = "code";

    private readonly IRepository<SsoProvider> _providerRepository;
    private readonly IRepository<SsoUserBinding> _bindingRepository;
    private readonly IConfigValueProtector _valueProtector;
    private readonly IUnitOfWork _unitOfWork;

    public SsoProviderService(
        IRepository<SsoProvider> providerRepository,
        IRepository<SsoUserBinding> bindingRepository,
        IConfigValueProtector valueProtector,
        IUnitOfWork unitOfWork)
    {
        _providerRepository = providerRepository;
        _bindingRepository = bindingRepository;
        _valueProtector = valueProtector;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<SsoProviderListResponse>> GetPagedAsync(
        SsoProviderQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _providerRepository.Query();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.ProviderCode.Contains(keyword) ||
                entity.ProviderName.Contains(keyword) ||
                (entity.Authority != null && entity.Authority.Contains(keyword)));
        }

        if (request.ProviderType.HasValue)
        {
            query = query.Where(entity => entity.ProviderType == request.ProviderType.Value);
        }

        if (request.Enabled.HasValue)
        {
            query = query.Where(entity => entity.Enabled == request.Enabled.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderBy(entity => entity.ProviderCode)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToListResponse)
            .ToList();

        return Task.FromResult(PagedResult<SsoProviderListResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public Task<IReadOnlyList<SsoProviderListResponse>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var items = _providerRepository.Query()
            .Where(entity => entity.Enabled)
            .OrderBy(entity => entity.ProviderCode)
            .ToList()
            .Select(ToListResponse)
            .ToList();

        return Task.FromResult<IReadOnlyList<SsoProviderListResponse>>(items);
    }

    public async Task<SsoProviderDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ToDetailResponse(await GetProviderOrThrowAsync(id, cancellationToken));
    }

    public async Task<SsoProviderDetailResponse> CreateAsync(
        CreateSsoProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantId(request.TenantId);
        ValidateLocalLoginFallback(request.AllowLocalLoginFallback);

        var providerCode = NormalizeCode(request.ProviderCode, "Provider code is required.");
        if (_providerRepository.Query().Any(entity =>
            entity.TenantId == request.TenantId && entity.ProviderCode == providerCode))
        {
            throw new BusinessException(ErrorCode.Conflict, "SSO provider code already exists in current tenant.");
        }

        ValidateProviderSettings(
            request.ProviderType,
            request.Authority,
            request.MetadataAddress,
            request.ClientId,
            request.CallbackPath,
            request.ResponseType);

        var provider = new SsoProvider
        {
            TenantId = request.TenantId,
            ProviderCode = providerCode,
            ProviderName = TrimRequired(request.ProviderName, "Provider name is required."),
            ProviderType = request.ProviderType,
            Enabled = request.Enabled,
            Authority = NormalizeOptional(request.Authority),
            MetadataAddress = NormalizeOptional(request.MetadataAddress),
            ClientId = NormalizeOptional(request.ClientId),
            ClientSecretEncrypted = ProtectSecret(request.ClientSecret),
            Scopes = NormalizeOptional(request.Scopes) ?? DefaultScopes,
            CallbackPath = NormalizeOptional(request.CallbackPath) ?? DefaultCallbackPath,
            ResponseType = NormalizeOptional(request.ResponseType) ?? DefaultResponseType,
            UsePkce = request.UsePkce,
            GetClaimsFromUserInfoEndpoint = request.GetClaimsFromUserInfoEndpoint,
            UserIdClaim = NormalizeOptional(request.UserIdClaim) ?? "sub",
            UserNameClaim = NormalizeOptional(request.UserNameClaim) ?? "preferred_username",
            EmailClaim = NormalizeOptional(request.EmailClaim) ?? "email",
            PhoneClaim = NormalizeOptional(request.PhoneClaim) ?? "phone_number",
            DisplayNameClaim = NormalizeOptional(request.DisplayNameClaim) ?? "name",
            RoleClaim = NormalizeOptional(request.RoleClaim) ?? "roles",
            DepartmentClaim = NormalizeOptional(request.DepartmentClaim) ?? "department",
            AutoCreateUser = request.AutoCreateUser,
            AutoBindUser = request.AutoBindUser,
            DefaultRoleIds = NormalizeOptional(request.DefaultRoleIds),
            AllowLocalLoginFallback = request.AllowLocalLoginFallback,
            LogoutRedirectUri = NormalizeOptional(request.LogoutRedirectUri),
            Remark = NormalizeOptional(request.Remark)
        };

        await _providerRepository.AddAsync(provider, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDetailResponse(provider);
    }

    public async Task<SsoProviderDetailResponse> UpdateAsync(
        Guid id,
        UpdateSsoProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateLocalLoginFallback(request.AllowLocalLoginFallback);
        var provider = await GetProviderOrThrowAsync(id, cancellationToken);
        var callbackPath = NormalizeOptional(request.CallbackPath) ?? DefaultCallbackPath;
        var responseType = NormalizeOptional(request.ResponseType) ?? DefaultResponseType;

        ValidateProviderSettings(
            request.ProviderType,
            request.Authority,
            request.MetadataAddress,
            request.ClientId,
            callbackPath,
            responseType);

        provider.ProviderName = TrimRequired(request.ProviderName, "Provider name is required.");
        provider.ProviderType = request.ProviderType;
        provider.Authority = NormalizeOptional(request.Authority);
        provider.MetadataAddress = NormalizeOptional(request.MetadataAddress);
        provider.ClientId = NormalizeOptional(request.ClientId);
        if (!string.IsNullOrWhiteSpace(request.ClientSecret) && request.ClientSecret.Trim() != MaskedSecret)
        {
            provider.ClientSecretEncrypted = ProtectSecret(request.ClientSecret);
        }

        provider.Scopes = NormalizeOptional(request.Scopes) ?? DefaultScopes;
        provider.CallbackPath = callbackPath;
        provider.ResponseType = responseType;
        provider.UsePkce = request.UsePkce;
        provider.GetClaimsFromUserInfoEndpoint = request.GetClaimsFromUserInfoEndpoint;
        provider.UserIdClaim = NormalizeOptional(request.UserIdClaim) ?? "sub";
        provider.UserNameClaim = NormalizeOptional(request.UserNameClaim) ?? "preferred_username";
        provider.EmailClaim = NormalizeOptional(request.EmailClaim) ?? "email";
        provider.PhoneClaim = NormalizeOptional(request.PhoneClaim) ?? "phone_number";
        provider.DisplayNameClaim = NormalizeOptional(request.DisplayNameClaim) ?? "name";
        provider.RoleClaim = NormalizeOptional(request.RoleClaim) ?? "roles";
        provider.DepartmentClaim = NormalizeOptional(request.DepartmentClaim) ?? "department";
        provider.AutoCreateUser = request.AutoCreateUser;
        provider.AutoBindUser = request.AutoBindUser;
        provider.DefaultRoleIds = NormalizeOptional(request.DefaultRoleIds);
        provider.AllowLocalLoginFallback = request.AllowLocalLoginFallback;
        provider.LogoutRedirectUri = NormalizeOptional(request.LogoutRedirectUri);
        provider.Remark = NormalizeOptional(request.Remark);

        _providerRepository.Update(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDetailResponse(provider);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(id, cancellationToken);
        if (_bindingRepository.Query().Any(entity => entity.ProviderId == provider.Id))
        {
            throw new BusinessException(ErrorCode.Conflict, "SSO provider has bound users and cannot be deleted.");
        }

        _providerRepository.Remove(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(id, cancellationToken);
        provider.Enabled = enabled;
        _providerRepository.Update(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SsoProviderTestResponse> TestAsync(
        Guid id,
        TestSsoProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(id, cancellationToken);
        var authority = NormalizeOptional(request.Authority) ?? provider.Authority;
        var metadataAddress = NormalizeOptional(request.MetadataAddress) ?? provider.MetadataAddress;
        var clientId = NormalizeOptional(request.ClientId) ?? provider.ClientId;

        ValidateProviderSettings(
            provider.ProviderType,
            authority,
            metadataAddress,
            clientId,
            provider.CallbackPath,
            provider.ResponseType);

        if (provider.ProviderType != SsoProviderType.Oidc)
        {
            return new SsoProviderTestResponse
            {
                Succeeded = true,
                Message = "Provider configuration validation succeeded. Metadata probe is only available for OIDC providers."
            };
        }

        var resolvedMetadataAddress = ResolveOidcMetadataAddress(authority, metadataAddress);
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        using var response = await httpClient.GetAsync(resolvedMetadataAddress, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new BusinessException(ErrorCode.BusinessError, $"OIDC metadata endpoint returned {(int)response.StatusCode}.");
        }

        return new SsoProviderTestResponse
        {
            Succeeded = true,
            Message = "OIDC provider metadata is reachable.",
            MetadataAddress = resolvedMetadataAddress
        };
    }

    private async Task<SsoProvider> GetProviderOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _providerRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "SSO provider was not found.");
    }

    private string? ProtectSecret(string? clientSecret)
    {
        return string.IsNullOrWhiteSpace(clientSecret)
            ? null
            : _valueProtector.Protect(clientSecret.Trim());
    }

    private static void ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Tenant id is required.");
        }
    }

    private static void ValidateLocalLoginFallback(bool allowLocalLoginFallback)
    {
        if (!allowLocalLoginFallback)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Local login fallback must remain enabled.");
        }
    }

    private static void ValidateProviderSettings(
        SsoProviderType providerType,
        string? authority,
        string? metadataAddress,
        string? clientId,
        string? callbackPath,
        string? responseType)
    {
        if (providerType == SsoProviderType.Oidc)
        {
            if (string.IsNullOrWhiteSpace(authority) && string.IsNullOrWhiteSpace(metadataAddress))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "OIDC authority or metadata address is required.");
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "OIDC client id is required.");
            }

            _ = ResolveOidcMetadataAddress(authority, metadataAddress);
        }

        if (string.IsNullOrWhiteSpace(callbackPath))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Callback path is required.");
        }

        if (string.IsNullOrWhiteSpace(responseType))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Response type is required.");
        }
    }

    private static string ResolveOidcMetadataAddress(string? authority, string? metadataAddress)
    {
        var metadata = NormalizeOptional(metadataAddress);
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            EnsureAbsoluteUri(metadata, "Metadata address must be an absolute URI.");
            return metadata;
        }

        var normalizedAuthority = NormalizeOptional(authority)
            ?? throw new BusinessException(ErrorCode.ValidationFailed, "OIDC authority is required.");
        EnsureAbsoluteUri(normalizedAuthority, "Authority must be an absolute URI.");
        return normalizedAuthority.TrimEnd('/') + "/.well-known/openid-configuration";
    }

    private static void EnsureAbsoluteUri(string value, string message)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }

    private static string NormalizeCode(string value, string message)
    {
        return TrimRequired(value, message).Trim().ToUpperInvariant();
    }

    private static string TrimRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static SsoProviderListResponse ToListResponse(SsoProvider entity)
    {
        return new SsoProviderListResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ProviderCode = entity.ProviderCode,
            ProviderName = entity.ProviderName,
            ProviderType = entity.ProviderType,
            Enabled = entity.Enabled,
            Authority = entity.Authority,
            MetadataAddress = entity.MetadataAddress,
            Scopes = entity.Scopes,
            CallbackPath = entity.CallbackPath,
            UsePkce = entity.UsePkce,
            AutoCreateUser = entity.AutoCreateUser,
            AutoBindUser = entity.AutoBindUser,
            AllowLocalLoginFallback = entity.AllowLocalLoginFallback,
            CreatedAt = entity.CreatedAt
        };
    }

    private static SsoProviderDetailResponse ToDetailResponse(SsoProvider entity)
    {
        return new SsoProviderDetailResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ProviderCode = entity.ProviderCode,
            ProviderName = entity.ProviderName,
            ProviderType = entity.ProviderType,
            Enabled = entity.Enabled,
            Authority = entity.Authority,
            MetadataAddress = entity.MetadataAddress,
            ClientId = entity.ClientId,
            ClientSecret = string.IsNullOrWhiteSpace(entity.ClientSecretEncrypted) ? string.Empty : MaskedSecret,
            HasClientSecret = !string.IsNullOrWhiteSpace(entity.ClientSecretEncrypted),
            Scopes = entity.Scopes,
            CallbackPath = entity.CallbackPath,
            ResponseType = entity.ResponseType,
            UsePkce = entity.UsePkce,
            GetClaimsFromUserInfoEndpoint = entity.GetClaimsFromUserInfoEndpoint,
            UserIdClaim = entity.UserIdClaim,
            UserNameClaim = entity.UserNameClaim,
            EmailClaim = entity.EmailClaim,
            PhoneClaim = entity.PhoneClaim,
            DisplayNameClaim = entity.DisplayNameClaim,
            RoleClaim = entity.RoleClaim,
            DepartmentClaim = entity.DepartmentClaim,
            AutoCreateUser = entity.AutoCreateUser,
            AutoBindUser = entity.AutoBindUser,
            DefaultRoleIds = entity.DefaultRoleIds,
            AllowLocalLoginFallback = entity.AllowLocalLoginFallback,
            LogoutRedirectUri = entity.LogoutRedirectUri,
            Remark = entity.Remark,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
