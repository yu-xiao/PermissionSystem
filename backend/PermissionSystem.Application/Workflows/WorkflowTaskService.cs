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
    private readonly IAsyncQueryExecutor _asyncQueryExecutor;

    public WorkflowTaskService(
        IRepository<WorkflowInstance> instanceRepository,
        IRepository<WorkflowTask> taskRepository,
        IRepository<WorkflowRecord> recordRepository,
        IRepository<WorkflowCc> ccRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IAsyncQueryExecutor asyncQueryExecutor)
    {
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _recordRepository = recordRepository;
        _ccRepository = ccRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _asyncQueryExecutor = asyncQueryExecutor;
    }

    public Task<PagedResult<WorkflowTaskResponse>> GetTodoAsync(
        WorkflowTaskQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        return GetTasksAsync(userId, WorkflowTaskStatus.Pending, completed: false, request, cancellationToken);
    }

    public Task<PagedResult<WorkflowTaskResponse>> GetDoneAsync(
        WorkflowTaskQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        return GetTasksAsync(userId, WorkflowTaskStatus.Pending, completed: true, request, cancellationToken);
    }

    public async Task<PagedResult<WorkflowInstanceResponse>> GetMyStartedAsync(
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

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var items = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderByDescending(entity => entity.CreatedAt)
                .Skip(request.Skip)
                .Take(request.PageSize)
                .Select(entity => new WorkflowInstanceResponse
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
                    StartedAt = entity.StartedAt,
                    CompletedAt = entity.CompletedAt,
                    CreatedAt = entity.CreatedAt
                }),
            cancellationToken);

        return PagedResult<WorkflowInstanceResponse>.Create(items, request.PageIndex, request.PageSize, totalCount);
    }

    public async Task<PagedResult<WorkflowCcResponse>> GetMyCcAsync(
        WorkflowCcQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var query =
            from cc in _ccRepository.Query()
            join instance in _instanceRepository.Query() on cc.InstanceId equals instance.Id
            where cc.CcUserId == userId
            select new { Cc = cc, Instance = instance };

        if (request.IsRead.HasValue)
        {
            query = query.Where(entity => entity.Cc.IsRead == request.IsRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.Instance.BusinessTitle.Contains(keyword) ||
                entity.Instance.BusinessId.Contains(keyword) ||
                entity.Instance.DefinitionName.Contains(keyword) ||
                entity.Instance.StarterUserName.Contains(keyword) ||
                entity.Cc.NodeKey.Contains(keyword));
        }

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var items = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderByDescending(entity => entity.Cc.CreatedAt)
                .Skip(request.Skip)
                .Take(request.PageSize)
                .Select(entity => new WorkflowCcResponse
                {
                    Id = entity.Cc.Id,
                    TenantId = entity.Cc.TenantId,
                    InstanceId = entity.Cc.InstanceId,
                    NodeKey = entity.Cc.NodeKey,
                    CcUserId = entity.Cc.CcUserId,
                    CcUserName = entity.Cc.CcUserName,
                    IsRead = entity.Cc.IsRead,
                    ReadAt = entity.Cc.ReadAt,
                    BusinessType = entity.Instance.BusinessType,
                    BusinessId = entity.Instance.BusinessId,
                    BusinessTitle = entity.Instance.BusinessTitle,
                    DefinitionName = entity.Instance.DefinitionName,
                    StarterUserName = entity.Instance.StarterUserName,
                    InstanceStatus = entity.Instance.Status,
                    CreatedAt = entity.Cc.CreatedAt
                }),
            cancellationToken);

        return PagedResult<WorkflowCcResponse>.Create(items, request.PageIndex, request.PageSize, totalCount);
    }

    public async Task<WorkflowInstanceDetailResponse> GetInstanceDetailAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        var instance = await GetInstanceOrThrowAsync(instanceId, cancellationToken);
        await EnsureCanViewInstanceAsync(instance, cancellationToken);

        var tasks = (await _asyncQueryExecutor.ToListAsync(
                _taskRepository.Query()
                    .Where(entity => entity.InstanceId == instance.Id)
                    .OrderBy(entity => entity.AssignedAt),
                cancellationToken))
            .Select(entity => ToTaskResponse(entity, instance))
            .ToList();
        var ccs = (await _asyncQueryExecutor.ToListAsync(
                _ccRepository.Query()
                    .Where(entity => entity.InstanceId == instance.Id)
                    .OrderBy(entity => entity.CreatedAt),
                cancellationToken))
            .Select(entity => ToCcResponse(entity, instance))
            .ToList();
        var records = (await _asyncQueryExecutor.ToListAsync(
                _recordRepository.Query()
                    .Where(entity => entity.InstanceId == instance.Id)
                    .OrderBy(entity => entity.OperatedAt),
                cancellationToken))
            .Select(ToRecordResponse)
            .ToList();

        return ToInstanceDetailResponse(instance, tasks, ccs, records);
    }

    public async Task<IReadOnlyCollection<WorkflowRecordResponse>> GetRecordsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        var instance = await GetInstanceOrThrowAsync(instanceId, cancellationToken);
        await EnsureCanViewInstanceAsync(instance, cancellationToken);

        return await _asyncQueryExecutor.ToListAsync(
            _recordRepository.Query()
                .Where(entity => entity.InstanceId == instance.Id)
                .OrderBy(entity => entity.OperatedAt)
                .Select(entity => new WorkflowRecordResponse
                {
                    Id = entity.Id,
                    InstanceId = entity.InstanceId,
                    TaskId = entity.TaskId,
                    NodeKey = entity.NodeKey,
                    NodeName = entity.NodeName,
                    OperatorUserId = entity.OperatorUserId,
                    OperatorUserName = entity.OperatorUserName,
                    Action = entity.Action,
                    Comment = entity.Comment,
                    OperatedAt = entity.OperatedAt
                }),
            cancellationToken);
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

    private async Task<PagedResult<WorkflowTaskResponse>> GetTasksAsync(
        Guid userId,
        WorkflowTaskStatus pendingStatus,
        bool completed,
        WorkflowTaskQueryRequest request,
        CancellationToken cancellationToken)
    {
        var query =
            from task in _taskRepository.Query()
            join instance in _instanceRepository.Query() on task.InstanceId equals instance.Id
            where task.ApproverUserId == userId &&
                (completed ? task.Status != pendingStatus : task.Status == pendingStatus)
            select new { Task = task, Instance = instance };

        if (request.Status.HasValue)
        {
            query = query.Where(entity => entity.Task.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.Instance.BusinessTitle.Contains(keyword) ||
                entity.Instance.BusinessId.Contains(keyword) ||
                entity.Instance.DefinitionName.Contains(keyword) ||
                entity.Instance.StarterUserName.Contains(keyword) ||
                entity.Task.NodeName.Contains(keyword));
        }

        var totalCount = await _asyncQueryExecutor.LongCountAsync(query, cancellationToken);
        var items = await _asyncQueryExecutor.ToListAsync(
            query
                .OrderByDescending(entity => entity.Task.AssignedAt)
                .Skip(request.Skip)
                .Take(request.PageSize)
                .Select(entity => new WorkflowTaskResponse
                {
                    Id = entity.Task.Id,
                    TenantId = entity.Task.TenantId,
                    InstanceId = entity.Task.InstanceId,
                    NodeKey = entity.Task.NodeKey,
                    NodeName = entity.Task.NodeName,
                    ApproverUserId = entity.Task.ApproverUserId,
                    ApproverUserName = entity.Task.ApproverUserName,
                    Status = entity.Task.Status,
                    AssignedAt = entity.Task.AssignedAt,
                    CompletedAt = entity.Task.CompletedAt,
                    DueAt = entity.Task.DueAt,
                    BusinessType = entity.Instance.BusinessType,
                    BusinessId = entity.Instance.BusinessId,
                    BusinessTitle = entity.Instance.BusinessTitle,
                    DefinitionName = entity.Instance.DefinitionName,
                    StarterUserName = entity.Instance.StarterUserName,
                    InstanceStatus = entity.Instance.Status,
                    StartedAt = entity.Instance.StartedAt
                }),
            cancellationToken);

        return PagedResult<WorkflowTaskResponse>.Create(items, request.PageIndex, request.PageSize, totalCount);
    }

    private async Task<WorkflowInstance> GetInstanceOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _instanceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Workflow instance was not found.");
    }

    private async Task EnsureCanViewInstanceAsync(WorkflowInstance instance, CancellationToken cancellationToken)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            return;
        }

        var userId = RequireUserId();
        var related = instance.StarterUserId == userId;
        if (!related)
        {
            related = await _asyncQueryExecutor.AnyAsync(
                _taskRepository.Query()
                    .Where(entity => entity.InstanceId == instance.Id && entity.ApproverUserId == userId),
                cancellationToken);
        }

        if (!related)
        {
            related = await _asyncQueryExecutor.AnyAsync(
                _ccRepository.Query()
                    .Where(entity => entity.InstanceId == instance.Id && entity.CcUserId == userId),
                cancellationToken);
        }

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
