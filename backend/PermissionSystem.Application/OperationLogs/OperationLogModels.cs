using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.OperationLogs;

public sealed class OperationLogQueryRequest : PaginationRequest
{
    public Guid? TenantId { get; init; }

    public string? Keyword { get; init; }

    public string? UserName { get; init; }

    public string? Module { get; init; }

    public string? Action { get; init; }

    public string? RequestMethod { get; init; }

    public int? StatusCode { get; init; }

    public string? TraceId { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }
}

public sealed class CreateOperationLogRequest
{
    public Guid TenantId { get; init; }

    public Guid? UserId { get; init; }

    public string? UserName { get; init; }

    public string Module { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string? RequestPath { get; init; }

    public string RequestMethod { get; init; } = string.Empty;

    public string? RequestBody { get; init; }

    public string? ResponseBody { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public int StatusCode { get; init; }

    public long ElapsedMilliseconds { get; init; }

    public string? TraceId { get; init; }
}

public class OperationLogResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid? UserId { get; init; }

    public string? UserName { get; init; }

    public string Module { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string? RequestPath { get; init; }

    public string RequestMethod { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public int StatusCode { get; init; }

    public long ElapsedMilliseconds { get; init; }

    public string? TraceId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class OperationLogDetailResponse : OperationLogResponse
{
    public string? RequestBody { get; init; }

    public string? ResponseBody { get; init; }
}

public interface IOperationLogService
{
    Task<PagedResult<OperationLogResponse>> GetPagedAsync(OperationLogQueryRequest request, CancellationToken cancellationToken = default);

    Task<OperationLogDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task CreateAsync(CreateOperationLogRequest request, CancellationToken cancellationToken = default);
}
