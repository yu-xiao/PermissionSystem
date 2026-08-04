using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Tenants;

public sealed class TenantWriteResolver : ITenantWriteResolver
{
    private const string HeaderSource = "Header";
    private const string RequestSource = "Request";

    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;

    public TenantWriteResolver(
        ITenantContext tenantContext,
        ICurrentUserService currentUserService)
    {
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
    }

    public Guid ResolveTenantId(Guid? requestedTenantId = null)
    {
        var requested = requestedTenantId is { } value && value != Guid.Empty
            ? value
            : (Guid?)null;

        if (_currentUserService.IsSuperAdmin || _tenantContext.IsSuperAdmin)
        {
            return ResolveForSuperAdmin(requested);
        }

        var currentTenantId = _currentUserService.TenantId ?? _tenantContext.TenantId;
        if (!currentTenantId.HasValue || currentTenantId.Value == Guid.Empty)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Tenant context is required.");
        }

        if (requested.HasValue && requested.Value != currentTenantId.Value)
        {
            throw new BusinessException(ErrorCode.Forbidden, "Cross-tenant writes are not allowed.");
        }

        return currentTenantId.Value;
    }

    private Guid ResolveForSuperAdmin(Guid? requestedTenantId)
    {
        var contextTenantId = IsExplicitSelection(_tenantContext.Source)
            ? _tenantContext.TenantId
            : null;

        if (requestedTenantId.HasValue && contextTenantId.HasValue &&
            requestedTenantId.Value != contextTenantId.Value)
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "TenantId must match the explicitly selected tenant.");
        }

        if (requestedTenantId.HasValue)
        {
            _tenantContext.SetTenant(requestedTenantId.Value, RequestSource);
            return requestedTenantId.Value;
        }

        if (contextTenantId.HasValue && contextTenantId.Value != Guid.Empty)
        {
            return contextTenantId.Value;
        }

        throw new BusinessException(
            ErrorCode.ValidationFailed,
            "Super administrators must explicitly select a target tenant.");
    }

    private static bool IsExplicitSelection(string? source)
    {
        return string.Equals(source, HeaderSource, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, RequestSource, StringComparison.OrdinalIgnoreCase);
    }
}
