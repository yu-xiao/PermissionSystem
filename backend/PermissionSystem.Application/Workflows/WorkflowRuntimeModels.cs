using System.Text.Json;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Workflows;

public sealed class StartWorkflowInstanceRequest
{
    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public string BusinessTitle { get; init; } = string.Empty;

    public JsonElement? FormData { get; init; }

    public string? FormDataJson { get; init; }

    public string? Remark { get; init; }
}

public sealed class WorkflowTaskActionRequest
{
    public string? Comment { get; init; }
}

public sealed class TransferWorkflowTaskRequest
{
    public Guid TargetUserId { get; init; }

    public string? Comment { get; init; }
}

public sealed class AddSignWorkflowTaskRequest
{
    public Guid TargetUserId { get; init; }

    public string? Comment { get; init; }
}

public sealed class WorkflowTaskQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public WorkflowTaskStatus? Status { get; init; }
}

public sealed class WorkflowInstanceQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public WorkflowInstanceStatus? Status { get; init; }
}

public sealed class WorkflowCcQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public bool? IsRead { get; init; }
}

public class WorkflowInstanceResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid DefinitionId { get; init; }

    public string DefinitionCode { get; init; } = string.Empty;

    public string DefinitionName { get; init; } = string.Empty;

    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public string BusinessTitle { get; init; } = string.Empty;

    public Guid StarterUserId { get; init; }

    public string StarterUserName { get; init; } = string.Empty;

    public WorkflowInstanceStatus Status { get; init; }

    public string? CurrentNodeKey { get; init; }

    public string? FormDataJson { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class WorkflowInstanceDetailResponse : WorkflowInstanceResponse
{
    public IReadOnlyCollection<WorkflowTaskResponse> Tasks { get; init; } = [];

    public IReadOnlyCollection<WorkflowCcResponse> Ccs { get; init; } = [];

    public IReadOnlyCollection<WorkflowRecordResponse> Records { get; init; } = [];
}

public sealed class WorkflowTaskResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid InstanceId { get; init; }

    public string NodeKey { get; init; } = string.Empty;

    public string NodeName { get; init; } = string.Empty;

    public Guid ApproverUserId { get; init; }

    public string ApproverUserName { get; init; } = string.Empty;

    public WorkflowTaskStatus Status { get; init; }

    public DateTimeOffset AssignedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public DateTimeOffset? DueAt { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public string BusinessTitle { get; init; } = string.Empty;

    public string DefinitionName { get; init; } = string.Empty;

    public string StarterUserName { get; init; } = string.Empty;

    public WorkflowInstanceStatus InstanceStatus { get; init; }

    public DateTimeOffset? StartedAt { get; init; }
}

public sealed class WorkflowRecordResponse
{
    public Guid Id { get; init; }

    public Guid InstanceId { get; init; }

    public Guid? TaskId { get; init; }

    public string? NodeKey { get; init; }

    public string? NodeName { get; init; }

    public Guid? OperatorUserId { get; init; }

    public string? OperatorUserName { get; init; }

    public WorkflowActionType Action { get; init; }

    public string? Comment { get; init; }

    public DateTimeOffset OperatedAt { get; init; }
}

public sealed class WorkflowCcResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid InstanceId { get; init; }

    public string NodeKey { get; init; } = string.Empty;

    public Guid CcUserId { get; init; }

    public string CcUserName { get; init; } = string.Empty;

    public bool IsRead { get; init; }

    public DateTimeOffset? ReadAt { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public string BusinessTitle { get; init; } = string.Empty;

    public string DefinitionName { get; init; } = string.Empty;

    public string StarterUserName { get; init; } = string.Empty;

    public WorkflowInstanceStatus InstanceStatus { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public interface IWorkflowEngine
{
    Task<WorkflowInstanceDetailResponse> StartAsync(
        StartWorkflowInstanceRequest request,
        CancellationToken cancellationToken = default);

    Task ApproveAsync(Guid taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task RejectAsync(Guid taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task WithdrawAsync(Guid instanceId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task TransferAsync(Guid taskId, TransferWorkflowTaskRequest request, CancellationToken cancellationToken = default);

    Task AddSignAsync(Guid taskId, AddSignWorkflowTaskRequest request, CancellationToken cancellationToken = default);
}

public interface IWorkflowTaskService
{
    Task<PagedResult<WorkflowTaskResponse>> GetTodoAsync(
        WorkflowTaskQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WorkflowTaskResponse>> GetDoneAsync(
        WorkflowTaskQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WorkflowInstanceResponse>> GetMyStartedAsync(
        WorkflowInstanceQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WorkflowCcResponse>> GetMyCcAsync(
        WorkflowCcQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstanceDetailResponse> GetInstanceDetailAsync(Guid instanceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<WorkflowRecordResponse>> GetRecordsAsync(Guid instanceId, CancellationToken cancellationToken = default);

    Task MarkCcAsReadAsync(Guid ccId, CancellationToken cancellationToken = default);
}

public interface IWorkflowConditionEvaluator
{
    bool Evaluate(string? expressionJson, string? formDataJson);
}

public interface IWorkflowApproverResolver
{
    IReadOnlyList<Guid> ResolveApproverUserIds(
        WorkflowNode node,
        WorkflowInstance instance,
        string? formDataJson);
}
