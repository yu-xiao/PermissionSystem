using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Workflows;

public sealed class WorkflowBusinessBindingQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? BusinessType { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreateWorkflowBusinessBindingRequest
{
    public Guid? TenantId { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string BusinessName { get; init; } = string.Empty;

    public Guid DefinitionId { get; init; }

    public bool IsEnabled { get; init; }

    public string? Remark { get; init; }
}

public sealed class UpdateWorkflowBusinessBindingRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string BusinessName { get; init; } = string.Empty;

    public Guid DefinitionId { get; init; }

    public string? Remark { get; init; }
}

public sealed class WorkflowBusinessBindingResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string BusinessName { get; init; } = string.Empty;

    public Guid DefinitionId { get; init; }

    public string DefinitionCode { get; init; } = string.Empty;

    public string DefinitionName { get; init; } = string.Empty;

    public int DefinitionVersion { get; init; }

    public WorkflowDefinitionStatus DefinitionStatus { get; init; }

    public bool IsEnabled { get; init; }

    public string? Remark { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public interface IWorkflowBusinessBindingService
{
    Task<PagedResult<WorkflowBusinessBindingResponse>> GetPagedAsync(
        WorkflowBusinessBindingQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowBusinessBindingResponse> CreateAsync(
        CreateWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowBusinessBindingResponse> UpdateAsync(
        Guid id,
        UpdateWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkflowBusinessBindingResponse> EnableAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkflowBusinessBindingResponse> DisableAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkflowBusinessBindingResponse> GetEnabledByBusinessTypeAsync(
        string businessType,
        CancellationToken cancellationToken = default);
}
