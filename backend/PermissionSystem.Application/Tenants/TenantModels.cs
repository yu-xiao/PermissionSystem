using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Tenants;

public sealed class TenantQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public bool? IsEnabled { get; init; }

    public TenantStatus? Status { get; init; }
}

public sealed class CreateTenantRequest
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string AdministratorUserName { get; init; } = "admin";

    public string AdministratorDisplayName { get; init; } = "租户管理员";

    public string AdministratorPassword { get; init; } = string.Empty;
}

public sealed class UpdateTenantRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
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

    public TenantStatus Status { get; init; }

    public string? InitializationStep { get; init; }

    public int InitializationProgress { get; init; }

    public int InitializationAttempts { get; init; }

    public string? InitializationError { get; init; }

    public DateTimeOffset? InitializationStartedAt { get; init; }

    public DateTimeOffset? InitializedAt { get; init; }

    public DateTimeOffset StatusChangedAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public interface ITenantService
{
    Task<PagedResult<TenantResponse>> GetPagedAsync(TenantQueryRequest request, CancellationToken cancellationToken = default);

    Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);

    Task<TenantResponse> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(Guid id, SetTenantEnabledRequest request, CancellationToken cancellationToken = default);

    Task RetryInitializationAsync(Guid id, CancellationToken cancellationToken = default);

    Task DisableAsync(Guid id, CancellationToken cancellationToken = default);

    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
