using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Sso;

public sealed class SsoUserBindingQueryRequest : PaginationRequest
{
    public Guid? ProviderId { get; init; }

    public string? Keyword { get; init; }
}

public class SsoUserBindingResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid ProviderId { get; init; }

    public string ProviderCode { get; init; } = string.Empty;

    public string? ProviderName { get; init; }

    public string ExternalUserId { get; init; } = string.Empty;

    public string? ExternalUserName { get; init; }

    public string? ExternalEmail { get; init; }

    public string? ExternalPhone { get; init; }

    public Guid LocalUserId { get; init; }

    public string? LocalUserName { get; init; }

    public string? LocalDisplayName { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class SsoUserBindingDetailResponse : SsoUserBindingResponse
{
    public string? ClaimsJson { get; init; }
}

public sealed class SsoRoleMappingRequest
{
    public string ExternalRole { get; init; } = string.Empty;

    public Guid LocalRoleId { get; init; }
}

public sealed class SsoRoleMappingResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid ProviderId { get; init; }

    public string ExternalRole { get; init; } = string.Empty;

    public Guid LocalRoleId { get; init; }

    public string? LocalRoleCode { get; init; }

    public string? LocalRoleName { get; init; }
}

public sealed class SsoDepartmentMappingRequest
{
    public string ExternalDepartment { get; init; } = string.Empty;

    public Guid LocalDepartmentId { get; init; }
}

public sealed class SsoDepartmentMappingResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid ProviderId { get; init; }

    public string ExternalDepartment { get; init; } = string.Empty;

    public Guid LocalDepartmentId { get; init; }

    public string? LocalDepartmentCode { get; init; }

    public string? LocalDepartmentName { get; init; }
}

public sealed class SsoLoginLogQueryRequest : PaginationRequest
{
    public string? ProviderCode { get; init; }

    public SsoProviderType? ProviderType { get; init; }

    public SsoLoginResult? LoginResult { get; init; }

    public string? Keyword { get; init; }

    public DateTimeOffset? StartAt { get; init; }

    public DateTimeOffset? EndAt { get; init; }
}

public sealed class SsoLoginLogResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string ProviderCode { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public SsoProviderType ProviderType { get; init; }

    public string? ExternalUserId { get; init; }

    public string? ExternalUserName { get; init; }

    public Guid? LocalUserId { get; init; }

    public string? LocalUserName { get; init; }

    public SsoLoginResult LoginResult { get; init; }

    public string? FailureReason { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? TraceId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public interface ISsoManagementService
{
    Task<PagedResult<SsoUserBindingResponse>> GetUserBindingsAsync(
        SsoUserBindingQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<SsoUserBindingDetailResponse> GetUserBindingAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteUserBindingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SsoRoleMappingResponse>> GetRoleMappingsAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SsoRoleMappingResponse>> SaveRoleMappingsAsync(
        Guid providerId,
        IReadOnlyCollection<SsoRoleMappingRequest> request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SsoDepartmentMappingResponse>> GetDepartmentMappingsAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SsoDepartmentMappingResponse>> SaveDepartmentMappingsAsync(
        Guid providerId,
        IReadOnlyCollection<SsoDepartmentMappingRequest> request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<SsoLoginLogResponse>> GetLoginLogsAsync(
        SsoLoginLogQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<SsoLoginLogResponse> GetLoginLogAsync(Guid id, CancellationToken cancellationToken = default);
}
