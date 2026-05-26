using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Workflows;

public sealed class WorkflowTaskService : IWorkflowTaskService
{
    private readonly IRepository<WorkflowInstance> _instanceRepository;
    private readonly IRepository<WorkflowTask> _taskRepository;
    private readonly IRepository<WorkflowRecord> _recordRepository;
    private readonly IRepository<WorkflowCc> _ccRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowTaskService(
        IRepository<WorkflowInstance> instanceRepository,
        IRepository<WorkflowTask> taskRepository,
        IRepository<WorkflowRecord> recordRepository,
        IRepository<WorkflowCc> ccRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _recordRepository = recordRepository;
        _ccRepository = ccRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<WorkflowTaskResponse>> GetTodoAsync(
        WorkflowTaskQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var query = _taskRepository.Query()
            .Where(entity => entity.ApproverUserId == userId && entity.Status == WorkflowTaskStatus.Pending);

        query = ApplyTaskQuery(query, request);
        return Task.FromResult(BuildTaskPagedResult(query, request));
    }

    public Task<PagedResult<WorkflowTaskResponse>> GetDoneAsync(
        WorkflowTaskQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var query = _taskRepository.Query()
            .Where(entity => entity.ApproverUserId == userId && entity.Status != WorkflowTaskStatus.Pending);

        query = ApplyTaskQuery(query, request);
        return Task.FromResult(BuildTaskPagedResult(query, request));
    }

    public Task<PagedResult<WorkflowInstanceResponse>> GetMyStartedAsync(
        WorkflowInstanceQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var query = _instanceRepository.Query()
            .Where(entity => entity.StarterUserId == userId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.BusinessTitle.Contains(keyword) ||
                entity.BusinessId.Contains(keyword) ||
                entity.DefinitionName.Contains(keyword));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(entity => entity.Status == request.Status.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToInstanceResponse)
            .ToList();

        return Task.FromResult(PagedResult<WorkflowInstanceResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public Task<PagedResult<WorkflowCcResponse>> GetMyCcAsync(
        WorkflowCcQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var query = _ccRepository.Query()
            .Where(entity => entity.CcUserId == userId);

        if (request.IsRead.HasValue)
        {
            query = query.Where(entity => entity.IsRead == request.IsRead.Value);
        }

        var rows = query
            .OrderByDescending(entity => entity.CreatedAt)
            .ToList();
        var instances = LoadInstances(rows.Select(entity => entity.InstanceId));
        var matchedItems = rows
            .Select(entity => ToCcResponse(entity, instances.GetValueOrDefault(entity.InstanceId)))
            .Where(entity => string.IsNullOrWhiteSpace(request.Keyword) || MatchesKeyword(entity, request.Keyword))
            .ToList();
        var totalCount = matchedItems.Count;
        var items = matchedItems
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList();

        return Task.FromResult(PagedResult<WorkflowCcResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<WorkflowInstanceDetailResponse> GetInstanceDetailAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        var instance = await GetInstanceOrThrowAsync(instanceId, cancellationToken);
        EnsureCanViewInstance(instance);

        var tasks = _taskRepository.Query()
            .Where(entity => entity.InstanceId == instance.Id)
            .OrderBy(entity => entity.AssignedAt)
            .ToList()
            .Select(entity => ToTaskResponse(entity, instance))
            .ToList();
        var ccs = _ccRepository.Query()
            .Where(entity => entity.InstanceId == instance.Id)
            .OrderBy(entity => entity.CreatedAt)
            .ToList()
            .Select(entity => ToCcResponse(entity, instance))
            .ToList();
        var records = _recordRepository.Query()
            .Where(entity => entity.InstanceId == instance.Id)
            .OrderBy(entity => entity.OperatedAt)
            .ToList()
            .Select(ToRecordResponse)
            .ToList();

        return ToInstanceDetailResponse(instance, tasks, ccs, records);
    }

    public async Task<IReadOnlyCollection<WorkflowRecordResponse>> GetRecordsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        var instance = await GetInstanceOrThrowAsync(instanceId, cancellationToken);
        EnsureCanViewInstance(instance);

        return _recordRepository.Query()
            .Where(entity => entity.InstanceId == instance.Id)
            .OrderBy(entity => entity.OperatedAt)
            .ToList()
            .Select(ToRecordResponse)
            .ToList();
    }

    public async Task MarkCcAsReadAsync(Guid ccId, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var cc = await _ccRepository.GetByIdAsync(ccId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Workflow cc was not found.");

        if (cc.CcUserId != userId)
        {
            throw new BusinessException(ErrorCode.Forbidden, "You are not allowed to update this workflow cc.");
        }

        if (!cc.IsRead)
        {
            cc.IsRead = true;
            cc.ReadAt = DateTimeOffset.UtcNow;
            _ccRepository.Update(cc);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private PagedResult<WorkflowTaskResponse> BuildTaskPagedResult(
        IQueryable<WorkflowTask> query,
        WorkflowTaskQueryRequest request)
    {
        var rows = query
            .OrderByDescending(entity => entity.AssignedAt)
            .ToList();
        var instances = LoadInstances(rows.Select(entity => entity.InstanceId));
        var matchedItems = rows
            .Select(entity => ToTaskResponse(entity, instances.GetValueOrDefault(entity.InstanceId)))
            .Where(entity => string.IsNullOrWhiteSpace(request.Keyword) || MatchesKeyword(entity, request.Keyword))
            .ToList();
        var totalCount = matchedItems.Count;
        var items = matchedItems
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList();

        return PagedResult<WorkflowTaskResponse>.Create(items, request.PageIndex, request.PageSize, totalCount);
    }

    private static IQueryable<WorkflowTask> ApplyTaskQuery(
        IQueryable<WorkflowTask> query,
        WorkflowTaskQueryRequest request)
    {
        if (request.Status.HasValue)
        {
            query = query.Where(entity => entity.Status == request.Status.Value);
        }

        return query;
    }

    private Dictionary<Guid, WorkflowInstance> LoadInstances(IEnumerable<Guid> instanceIds)
    {
        var ids = instanceIds.Distinct().ToArray();
        return _instanceRepository.Query()
            .Where(entity => ids.Contains(entity.Id))
            .ToDictionary(entity => entity.Id);
    }

    private async Task<WorkflowInstance> GetInstanceOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _instanceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Workflow instance was not found.");
    }

    private void EnsureCanViewInstance(WorkflowInstance instance)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            return;
        }

        var userId = RequireUserId();
        var related = instance.StarterUserId == userId ||
            _taskRepository.Query().Any(entity => entity.InstanceId == instance.Id && entity.ApproverUserId == userId) ||
            _ccRepository.Query().Any(entity => entity.InstanceId == instance.Id && entity.CcUserId == userId);

        if (!related)
        {
            throw new BusinessException(ErrorCode.Forbidden, "You are not allowed to view this workflow instance.");
        }
    }

    private Guid RequireUserId()
    {
        return _currentUserService.UserId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "User is not authenticated.");
    }

    private static WorkflowInstanceResponse ToInstanceResponse(WorkflowInstance entity)
    {
        return new WorkflowInstanceResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            DefinitionId = entity.DefinitionId,
            DefinitionCode = entity.DefinitionCode,
            DefinitionName = entity.DefinitionName,
            BusinessType = entity.BusinessType,
            BusinessId = entity.BusinessId,
            BusinessTitle = entity.BusinessTitle,
            StarterUserId = entity.StarterUserId,
            StarterUserName = entity.StarterUserName,
            Status = entity.Status,
            CurrentNodeKey = entity.CurrentNodeKey,
            FormDataJson = entity.FormDataJson,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            CreatedAt = entity.CreatedAt
        };
    }

    private static WorkflowInstanceDetailResponse ToInstanceDetailResponse(
        WorkflowInstance entity,
        IReadOnlyCollection<WorkflowTaskResponse> tasks,
        IReadOnlyCollection<WorkflowCcResponse> ccs,
        IReadOnlyCollection<WorkflowRecordResponse> records)
    {
        return new WorkflowInstanceDetailResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            DefinitionId = entity.DefinitionId,
            DefinitionCode = entity.DefinitionCode,
            DefinitionName = entity.DefinitionName,
            BusinessType = entity.BusinessType,
            BusinessId = entity.BusinessId,
            BusinessTitle = entity.BusinessTitle,
            StarterUserId = entity.StarterUserId,
            StarterUserName = entity.StarterUserName,
            Status = entity.Status,
            CurrentNodeKey = entity.CurrentNodeKey,
            FormDataJson = entity.FormDataJson,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            CreatedAt = entity.CreatedAt,
            Tasks = tasks,
            Ccs = ccs,
            Records = records
        };
    }

    private static WorkflowTaskResponse ToTaskResponse(WorkflowTask task, WorkflowInstance? instance)
    {
        return new WorkflowTaskResponse
        {
            Id = task.Id,
            TenantId = task.TenantId,
            InstanceId = task.InstanceId,
            NodeKey = task.NodeKey,
            NodeName = task.NodeName,
            ApproverUserId = task.ApproverUserId,
            ApproverUserName = task.ApproverUserName,
            Status = task.Status,
            AssignedAt = task.AssignedAt,
            CompletedAt = task.CompletedAt,
            DueAt = task.DueAt,
            BusinessType = instance?.BusinessType ?? string.Empty,
            BusinessId = instance?.BusinessId ?? string.Empty,
            BusinessTitle = instance?.BusinessTitle ?? string.Empty,
            DefinitionName = instance?.DefinitionName ?? string.Empty,
            StarterUserName = instance?.StarterUserName ?? string.Empty,
            InstanceStatus = instance?.Status ?? WorkflowInstanceStatus.Running,
            StartedAt = instance?.StartedAt
        };
    }

    private static WorkflowCcResponse ToCcResponse(WorkflowCc cc, WorkflowInstance? instance)
    {
        return new WorkflowCcResponse
        {
            Id = cc.Id,
            TenantId = cc.TenantId,
            InstanceId = cc.InstanceId,
            NodeKey = cc.NodeKey,
            CcUserId = cc.CcUserId,
            CcUserName = cc.CcUserName,
            IsRead = cc.IsRead,
            ReadAt = cc.ReadAt,
            BusinessType = instance?.BusinessType ?? string.Empty,
            BusinessId = instance?.BusinessId ?? string.Empty,
            BusinessTitle = instance?.BusinessTitle ?? string.Empty,
            DefinitionName = instance?.DefinitionName ?? string.Empty,
            StarterUserName = instance?.StarterUserName ?? string.Empty,
            InstanceStatus = instance?.Status ?? WorkflowInstanceStatus.Running,
            CreatedAt = cc.CreatedAt
        };
    }

    private static WorkflowRecordResponse ToRecordResponse(WorkflowRecord record)
    {
        return new WorkflowRecordResponse
        {
            Id = record.Id,
            InstanceId = record.InstanceId,
            TaskId = record.TaskId,
            NodeKey = record.NodeKey,
            NodeName = record.NodeName,
            OperatorUserId = record.OperatorUserId,
            OperatorUserName = record.OperatorUserName,
            Action = record.Action,
            Comment = record.Comment,
            OperatedAt = record.OperatedAt
        };
    }

    private static bool MatchesKeyword(WorkflowTaskResponse task, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        var value = keyword.Trim();
        return task.BusinessTitle.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            task.BusinessId.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            task.DefinitionName.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            task.StarterUserName.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            task.NodeName.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesKeyword(WorkflowCcResponse cc, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        var value = keyword.Trim();
        return cc.BusinessTitle.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            cc.BusinessId.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            cc.DefinitionName.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            cc.StarterUserName.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            cc.NodeKey.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
