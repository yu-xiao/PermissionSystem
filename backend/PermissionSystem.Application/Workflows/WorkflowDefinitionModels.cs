using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Workflows;

public sealed class WorkflowDefinitionQueryRequest : PaginationRequest
{
    public Guid? TenantId { get; init; }

    public string? Keyword { get; init; }

    public WorkflowDefinitionStatus? Status { get; init; }

    public bool? IsPublished { get; init; }
}

public sealed class CreateWorkflowDefinitionRequest
{
    public Guid? TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? BusinessType { get; init; }
}

public sealed class UpdateWorkflowDefinitionRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? BusinessType { get; init; }
}

public sealed class SaveWorkflowDesignerRequest
{
    public IReadOnlyCollection<WorkflowDesignerNodeRequest> Nodes { get; init; } = [];

    public IReadOnlyCollection<WorkflowDesignerEdgeRequest> Edges { get; init; } = [];

    public IReadOnlyCollection<WorkflowDesignerConditionRequest> Conditions { get; init; } = [];
}

public sealed class WorkflowDesignerNodeRequest
{
    public Guid? Id { get; init; }

    public string NodeKey { get; init; } = string.Empty;

    public string NodeName { get; init; } = string.Empty;

    public WorkflowNodeType NodeType { get; init; }

    public WorkflowApproverType? ApproverType { get; init; }

    public string? ApproverIds { get; init; }

    public WorkflowApprovalMode? ApprovalMode { get; init; }

    public string? ConfigJson { get; init; }

    public decimal PositionX { get; init; }

    public decimal PositionY { get; init; }

    public int Sort { get; init; }
}

public sealed class WorkflowDesignerEdgeRequest
{
    public Guid? Id { get; init; }

    public string FromNodeKey { get; init; } = string.Empty;

    public string ToNodeKey { get; init; } = string.Empty;

    public Guid? ConditionId { get; init; }

    public bool IsDefault { get; init; }

    public int Sort { get; init; }
}

public sealed class WorkflowDesignerConditionRequest
{
    public Guid? Id { get; init; }

    public string NodeKey { get; init; } = string.Empty;

    public string ConditionName { get; init; } = string.Empty;

    public string ExpressionJson { get; init; } = string.Empty;

    public int Sort { get; init; }
}

public sealed class PublishWorkflowDefinitionRequest
{
    public string? Remark { get; init; }
}

public class WorkflowDefinitionListResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? BusinessType { get; init; }

    public int Version { get; init; }

    public WorkflowDefinitionStatus Status { get; init; }

    public bool IsPublished { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class WorkflowDefinitionDetailResponse : WorkflowDefinitionListResponse
{
    public WorkflowDesignerResponse Designer { get; init; } = new();
}

public sealed class WorkflowDesignerResponse
{
    public IReadOnlyCollection<WorkflowDesignerNodeResponse> Nodes { get; init; } = [];

    public IReadOnlyCollection<WorkflowDesignerEdgeResponse> Edges { get; init; } = [];

    public IReadOnlyCollection<WorkflowDesignerConditionResponse> Conditions { get; init; } = [];
}

public sealed class WorkflowDesignerNodeResponse
{
    public Guid Id { get; init; }

    public string NodeKey { get; init; } = string.Empty;

    public string NodeName { get; init; } = string.Empty;

    public WorkflowNodeType NodeType { get; init; }

    public WorkflowApproverType? ApproverType { get; init; }

    public string? ApproverIds { get; init; }

    public WorkflowApprovalMode? ApprovalMode { get; init; }

    public string? ConfigJson { get; init; }

    public decimal PositionX { get; init; }

    public decimal PositionY { get; init; }

    public int Sort { get; init; }
}

public sealed class WorkflowDesignerEdgeResponse
{
    public Guid Id { get; init; }

    public string FromNodeKey { get; init; } = string.Empty;

    public string ToNodeKey { get; init; } = string.Empty;

    public Guid? ConditionId { get; init; }

    public bool IsDefault { get; init; }

    public int Sort { get; init; }
}

public sealed class WorkflowDesignerConditionResponse
{
    public Guid Id { get; init; }

    public string NodeKey { get; init; } = string.Empty;

    public string ConditionName { get; init; } = string.Empty;

    public string ExpressionJson { get; init; } = string.Empty;

    public int Sort { get; init; }
}

public interface IWorkflowDefinitionService
{
    Task<PagedResult<WorkflowDefinitionListResponse>> GetPagedAsync(
        WorkflowDefinitionQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionListResponse> CreateAsync(
        CreateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionListResponse> UpdateAsync(
        Guid id,
        UpdateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkflowDesignerResponse> GetDesignerAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkflowDesignerResponse> SaveDesignerAsync(
        Guid id,
        SaveWorkflowDesignerRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionListResponse> PublishAsync(
        Guid id,
        PublishWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionListResponse> DisableAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionDetailResponse> CopyAsync(Guid id, CancellationToken cancellationToken = default);
}
