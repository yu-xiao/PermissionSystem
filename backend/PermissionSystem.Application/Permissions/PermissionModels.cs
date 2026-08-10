using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Permissions;

public sealed class PermissionQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? Group { get; init; }
}

public sealed class CreatePermissionRequest
{
    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Resource { get; init; }

    public string? Action { get; init; }
}

public sealed class UpdatePermissionRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Resource { get; init; }

    public string? Action { get; init; }
}

public sealed class PermissionResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Resource { get; init; }

    public string? Action { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public interface IPermissionService
{
    Task<PagedResult<PermissionResponse>> GetPagedAsync(PermissionQueryRequest request, CancellationToken cancellationToken = default);

    Task<PermissionResponse> CreateAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default);

    Task<PermissionResponse> UpdateAsync(Guid id, UpdatePermissionRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
