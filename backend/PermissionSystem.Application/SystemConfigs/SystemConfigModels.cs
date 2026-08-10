using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.SystemConfigs;

public sealed class SystemConfigQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? GroupCode { get; init; }

    public string? ConfigType { get; init; }

    public string? Status { get; init; }

    public bool? IsEncrypted { get; init; }

    public bool? IsSystem { get; init; }
}

public sealed class CreateSystemConfigRequest
{
    public Guid TenantId { get; init; }

    public string ConfigKey { get; init; } = string.Empty;

    public string ConfigValue { get; init; } = string.Empty;

    public string ConfigType { get; init; } = "String";

    public string GroupCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEncrypted { get; init; }

    public bool IsSystem { get; init; }

    public string Status { get; init; } = "Enabled";

    public int Sort { get; init; }
}

public sealed class UpdateSystemConfigRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string? ConfigValue { get; init; }

    public string ConfigType { get; init; } = "String";

    public string GroupCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEncrypted { get; init; }

    public bool IsSystem { get; init; }

    public string Status { get; init; } = "Enabled";

    public int Sort { get; init; }
}

public sealed class SystemConfigResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string ConfigKey { get; init; } = string.Empty;

    public string ConfigValue { get; init; } = string.Empty;

    public string ConfigType { get; init; } = string.Empty;

    public string GroupCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsEncrypted { get; init; }

    public bool IsSystem { get; init; }

    public string Status { get; init; } = string.Empty;

    public int Sort { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class SystemConfigValueResponse
{
    public string ConfigKey { get; init; } = string.Empty;

    public string ConfigValue { get; init; } = string.Empty;

    public string ConfigType { get; init; } = string.Empty;

    public bool IsEncrypted { get; init; }
}

public sealed class SystemConfigCacheEntry
{
    public Guid TenantId { get; init; }

    public string ConfigKey { get; init; } = string.Empty;

    public string ConfigValue { get; init; } = string.Empty;

    public string ConfigType { get; init; } = string.Empty;

    public bool IsEncrypted { get; init; }

    public string Status { get; init; } = string.Empty;
}

public interface ISystemConfigService
{
    Task<PagedResult<SystemConfigResponse>> GetPagedAsync(
        SystemConfigQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<SystemConfigResponse> CreateAsync(
        CreateSystemConfigRequest request,
        CancellationToken cancellationToken = default);

    Task<SystemConfigResponse> UpdateAsync(
        Guid id,
        UpdateSystemConfigRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SystemConfigValueResponse> GetValueByKeyAsync(
        string configKey,
        bool revealSensitive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemConfigResponse>> GetEnabledByGroupCodeAsync(
        string groupCode,
        CancellationToken cancellationToken = default);
}
