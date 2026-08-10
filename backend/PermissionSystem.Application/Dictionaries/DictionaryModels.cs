using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Dictionaries;

public sealed class DictionaryTypeQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? Status { get; init; }
}

public sealed class CreateDictionaryTypeRequest
{
    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Status { get; init; } = "Enabled";

    public int Sort { get; init; }
}

public sealed class UpdateDictionaryTypeRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Status { get; init; } = "Enabled";

    public int Sort { get; init; }
}

public sealed class DictionaryTypeResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Status { get; init; } = string.Empty;

    public int Sort { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class DictionaryItemQueryRequest : PaginationRequest
{
    public string? TypeCode { get; init; }

    public string? Keyword { get; init; }

    public string? Status { get; init; }
}

public sealed class CreateDictionaryItemRequest
{
    public Guid TenantId { get; init; }

    public string TypeCode { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string? Color { get; init; }

    public string? CssClass { get; init; }

    public bool IsDefault { get; init; }

    public string Status { get; init; } = "Enabled";

    public int Sort { get; init; }

    public string? Remark { get; init; }
}

public sealed class UpdateDictionaryItemRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string? Color { get; init; }

    public string? CssClass { get; init; }

    public bool IsDefault { get; init; }

    public string Status { get; init; } = "Enabled";

    public int Sort { get; init; }

    public string? Remark { get; init; }
}

public sealed class DictionaryItemResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string TypeCode { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string? Color { get; init; }

    public string? CssClass { get; init; }

    public bool IsDefault { get; init; }

    public string Status { get; init; } = string.Empty;

    public int Sort { get; init; }

    public string? Remark { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public interface IDictionaryService
{
    Task<PagedResult<DictionaryTypeResponse>> GetTypesPagedAsync(
        DictionaryTypeQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<DictionaryTypeResponse> CreateTypeAsync(
        CreateDictionaryTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<DictionaryTypeResponse> UpdateTypeAsync(
        Guid id,
        UpdateDictionaryTypeRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteTypeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<DictionaryItemResponse>> GetItemsPagedAsync(
        DictionaryItemQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<DictionaryItemResponse> CreateItemAsync(
        CreateDictionaryItemRequest request,
        CancellationToken cancellationToken = default);

    Task<DictionaryItemResponse> UpdateItemAsync(
        Guid id,
        UpdateDictionaryItemRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DictionaryItemResponse>> GetEnabledItemsByTypeCodeAsync(
        string typeCode,
        CancellationToken cancellationToken = default);
}
