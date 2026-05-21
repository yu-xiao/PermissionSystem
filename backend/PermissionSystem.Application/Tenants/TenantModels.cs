using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Tenants;

public sealed class TenantQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreateTenantRequest
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public sealed class UpdateTenantRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public sealed class SetTenantEnabledRequest
{
    public bool IsEnabled { get; init; }
}

public sealed class TenantResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public interface ITenantService
{
    Task<PagedResult<TenantResponse>> GetPagedAsync(TenantQueryRequest request, CancellationToken cancellationToken = default);

    Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);

    Task<TenantResponse> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(Guid id, SetTenantEnabledRequest request, CancellationToken cancellationToken = default);
}
