using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.LoginLogs;

public sealed class LoginLogQueryRequest : PaginationRequest
{
    public Guid? TenantId { get; init; }

    public string? Keyword { get; init; }

    public string? UserName { get; init; }

    public string? LoginType { get; init; }

    public string? LoginResult { get; init; }

    public string? TraceId { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }
}

public sealed class CreateLoginLogRequest
{
    public Guid TenantId { get; init; }

    public Guid? UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string LoginType { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string LoginResult { get; init; } = string.Empty;

    public string? FailureReason { get; init; }

    public string? TraceId { get; init; }
}

public sealed class LoginLogResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid? UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string LoginType { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string LoginResult { get; init; } = string.Empty;

    public string? FailureReason { get; init; }

    public string? TraceId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public interface ILoginLogService
{
    Task<PagedResult<LoginLogResponse>> GetPagedAsync(LoginLogQueryRequest request, CancellationToken cancellationToken = default);

    Task<LoginLogResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task CreateAsync(CreateLoginLogRequest request, CancellationToken cancellationToken = default);
}
