using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Workflows;

public sealed class WorkflowEngine : IWorkflowEngine
{
    private const int MaxNodeTransitions = 100;

    private readonly IRepository<WorkflowDefinition> _definitionRepository;
    private readonly IRepository<WorkflowBusinessBinding> _bindingRepository;
    private readonly IRepository<WorkflowNode> _nodeRepository;
    private readonly IRepository<WorkflowEdge> _edgeRepository;
    private readonly IRepository<WorkflowCondition> _conditionRepository;
    private readonly IRepository<WorkflowInstance> _instanceRepository;
    private readonly IRepository<WorkflowTask> _taskRepository;
    private readonly IRepository<WorkflowRecord> _recordRepository;
    private readonly IRepository<WorkflowCc> _ccRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IWorkflowConditionEvaluator _conditionEvaluator;
    private readonly IWorkflowApproverResolver _approverResolver;
    private readonly IWorkflowBusinessHandlerResolver _businessHandlerResolver;
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IRepository<WorkflowDefinition> definitionRepository,
        IRepository<WorkflowBusinessBinding> bindingRepository,
        IRepository<WorkflowNode> nodeRepository,
        IRepository<WorkflowEdge> edgeRepository,
        IRepository<WorkflowCondition> conditionRepository,
        IRepository<WorkflowInstance> instanceRepository,
        IRepository<WorkflowTask> taskRepository,
        IRepository<WorkflowRecord> recordRepository,
        IRepository<WorkflowCc> ccRepository,
        IRepository<User> userRepository,
        IWorkflowConditionEvaluator conditionEvaluator,
        IWorkflowApproverResolver approverResolver,
        IWorkflowBusinessHandlerResolver businessHandlerResolver,
        INotificationService notificationService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILogger<WorkflowEngine> logger)
    {
        _definitionRepository = definitionRepository;
        _bindingRepository = bindingRepository;
        _nodeRepository = nodeRepository;
        _edgeRepository = edgeRepository;
        _conditionRepository = conditionRepository;
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _recordRepository = recordRepository;
        _ccRepository = ccRepository;
        _userRepository = userRepository;
        _conditionEvaluator = conditionEvaluator;
        _approverResolver = approverResolver;
        _businessHandlerResolver = businessHandlerResolver;
        _notificationService = notificationService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<WorkflowInstanceDetailResponse> StartAsync(
        StartWorkflowInstanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var starterUserId = RequireUserId();
        var tenantId = RequireTenantId();
        var businessType = TrimRequired(request.BusinessType, "Business type is required.");
        var businessId = TrimRequired(request.BusinessId, "Business id is required.");
        var businessTitle = TrimRequired(request.BusinessTitle, "Business title is required.");
        var definition = GetPublishedDefinitionByBusinessType(tenantId, businessType);
        var formDataJson = ResolveFormDataJson(request);
        var businessHandler = _businessHandlerResolver.Resolve(businessType);
        var starterUserName = _currentUserService.Username ?? "Unknown";
        WorkflowInstance? instance = null;

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (_instanceRepository.Query().Any(entity =>
                entity.TenantId == tenantId &&
                entity.BusinessType == businessType &&
                entity.BusinessId == businessId &&
                entity.Status == WorkflowInstanceStatus.Running))
            {
                throw new BusinessException(ErrorCode.Conflict, "Business document already has a running workflow instance.");
            }

            instance = new WorkflowInstance
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DefinitionId = definition.Id,
                DefinitionCode = definition.Code,
                DefinitionName = definition.Name,
                BusinessType = businessType,
                BusinessId = businessId,
                BusinessTitle = businessTitle,
                StarterUserId = starterUserId,
                StarterUserName = starterUserName,
                Status = WorkflowInstanceStatus.Running,
                FormDataJson = formDataJson,
                StartedAt = DateTimeOffset.UtcNow
            };

            await _instanceRepository.AddAsync(instance, token);
            await businessHandler.OnWorkflowStartedAsync(
                BuildBusinessContext(instance, WorkflowActionType.Start, request.Remark ?? request.BusinessTitle),
                token);
            await AddRecordAsync(instance, null, null, WorkflowActionType.Start, request.Remark ?? request.BusinessTitle, token);

            var startNode = _nodeRepository.Query()
                .FirstOrDefault(entity => entity.DefinitionId == definition.Id && entity.NodeType == WorkflowNodeType.Start)
                ?? throw new BusinessException(ErrorCode.ValidationFailed, "Workflow definition does not contain a Start node.");

            await MoveNextAsync(instance, startNode, token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return BuildInstanceDetailResponse(instance!);
    }

    public async Task ApproveAsync(
        Guid taskId,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var task = await GetPendingTaskForCurrentUserAsync(taskId, userId, token);
            var instance = await GetRunningInstanceOrThrowAsync(task.InstanceId, token);
            var node = GetNodeOrThrow(instance.DefinitionId, task.NodeKey);
            var now = DateTimeOffset.UtcNow;

            task.Status = WorkflowTaskStatus.Approved;
            task.CompletedAt = now;
            _taskRepository.Update(task);
            await AddRecordAsync(instance, task, node, WorkflowActionType.Approve, request.Comment, token);

            await ContinueAfterApprovalAsync(instance, task, node, token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public async Task RejectAsync(
        Guid taskId,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var task = await GetPendingTaskForCurrentUserAsync(taskId, userId, token);
            var instance = await GetRunningInstanceOrThrowAsync(task.InstanceId, token);
            var node = GetNodeOrThrow(instance.DefinitionId, task.NodeKey);
            var now = DateTimeOffset.UtcNow;

            task.Status = WorkflowTaskStatus.Rejected;
            task.CompletedAt = now;
            _taskRepository.Update(task);

            instance.Status = WorkflowInstanceStatus.Rejected;
            instance.CompletedAt = now;
            _instanceRepository.Update(instance);

            ClosePendingTasks(instance.Id, task.Id);
            await AddRecordAsync(instance, task, node, WorkflowActionType.Reject, request.Comment, token);
            await _businessHandlerResolver.Resolve(instance.BusinessType)
                .OnWorkflowRejectedAsync(BuildBusinessContext(instance, WorkflowActionType.Reject, request.Comment), token);
            await NotifyStarterAsync(instance, "流程已拒绝", $"流程“{instance.BusinessTitle}”已被拒绝。", token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public async Task WithdrawAsync(
        Guid instanceId,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var instance = await _instanceRepository.GetByIdAsync(instanceId, token)
                ?? throw new BusinessException(ErrorCode.NotFound, "Workflow instance was not found.");

            if (instance.StarterUserId != userId)
            {
                throw new BusinessException(ErrorCode.Forbidden, "Only the starter can withdraw this workflow instance.");
            }

            if (instance.Status is WorkflowInstanceStatus.Approved or WorkflowInstanceStatus.Rejected or WorkflowInstanceStatus.Withdrawn)
            {
                throw new BusinessException(ErrorCode.Conflict, "Completed or rejected workflow instances cannot be withdrawn.");
            }

            instance.Status = WorkflowInstanceStatus.Withdrawn;
            instance.CompletedAt = DateTimeOffset.UtcNow;
            _instanceRepository.Update(instance);

            ClosePendingTasks(instance.Id, null);
            await AddRecordAsync(instance, null, null, WorkflowActionType.Withdraw, request.Comment, token);
            await _businessHandlerResolver.Resolve(instance.BusinessType)
                .OnWorkflowWithdrawnAsync(BuildBusinessContext(instance, WorkflowActionType.Withdraw, request.Comment), token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public async Task TransferAsync(
        Guid taskId,
        TransferWorkflowTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var task = await GetPendingTaskForCurrentUserAsync(taskId, userId, token);
            var instance = await GetRunningInstanceOrThrowAsync(task.InstanceId, token);
            var targetUser = GetEnabledUserOrThrow(instance.TenantId, request.TargetUserId);
            var oldApproverName = task.ApproverUserName;

            task.ApproverUserId = targetUser.Id;
            task.ApproverUserName = targetUser.DisplayName;
            task.AssignedAt = DateTimeOffset.UtcNow;
            _taskRepository.Update(task);

            await AddRecordAsync(
                instance,
                task,
                GetNodeOrThrow(instance.DefinitionId, task.NodeKey),
                WorkflowActionType.Transfer,
                string.IsNullOrWhiteSpace(request.Comment)
                    ? $"Transferred from {oldApproverName} to {targetUser.DisplayName}."
                    : request.Comment,
                token);
            await NotifyTaskAsync(instance, task, "新的审批任务", $"流程“{instance.BusinessTitle}”已转交给你审批。", token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public async Task AddSignAsync(
        Guid taskId,
        AddSignWorkflowTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var sourceTask = await GetPendingTaskForCurrentUserAsync(taskId, userId, token);
            var instance = await GetRunningInstanceOrThrowAsync(sourceTask.InstanceId, token);
            var targetUser = GetEnabledUserOrThrow(instance.TenantId, request.TargetUserId);

            var task = await CreateTaskAsync(instance, sourceTask.NodeKey, sourceTask.NodeName, targetUser.Id, targetUser.DisplayName, token);
            await AddRecordAsync(
                instance,
                sourceTask,
                GetNodeOrThrow(instance.DefinitionId, sourceTask.NodeKey),
                WorkflowActionType.AddSign,
                string.IsNullOrWhiteSpace(request.Comment)
                    ? $"Added approver {targetUser.DisplayName}."
                    : request.Comment,
                token);
            await NotifyTaskAsync(instance, task, "新的加签任务", $"流程“{instance.BusinessTitle}”需要你加签审批。", token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);
    }

    private async Task ContinueAfterApprovalAsync(
        WorkflowInstance instance,
        WorkflowTask task,
        WorkflowNode node,
        CancellationToken cancellationToken)
    {
        var mode = node.ApprovalMode ?? WorkflowApprovalMode.Single;
        if (mode == WorkflowApprovalMode.Countersign &&
            _taskRepository.Query().Any(entity =>
                entity.InstanceId == instance.Id &&
                entity.NodeKey == node.NodeKey &&
                entity.Status == WorkflowTaskStatus.Pending))
        {
            return;
        }

        if (mode == WorkflowApprovalMode.Sequential)
        {
            var approverIds = _approverResolver.ResolveApproverUserIds(node, instance, instance.FormDataJson);
            var approverList = approverIds.ToList();
            var currentIndex = approverList.IndexOf(task.ApproverUserId);
            var nextUserId = currentIndex >= 0 && currentIndex + 1 < approverIds.Count
                ? approverList[currentIndex + 1]
                : Guid.Empty;

            if (nextUserId != Guid.Empty)
            {
                var nextUser = GetEnabledUserOrThrow(instance.TenantId, nextUserId);
                await CreateTaskAsync(instance, node.NodeKey, node.NodeName, nextUser.Id, nextUser.DisplayName, cancellationToken);
                return;
            }
        }

        if (mode is WorkflowApprovalMode.OrSign or WorkflowApprovalMode.Single)
        {
            foreach (var otherTask in _taskRepository.Query()
                .Where(entity => entity.InstanceId == instance.Id &&
                    entity.NodeKey == node.NodeKey &&
                    entity.Id != task.Id &&
                    entity.Status == WorkflowTaskStatus.Pending)
                .ToList())
            {
                otherTask.Status = WorkflowTaskStatus.Canceled;
                otherTask.CompletedAt = DateTimeOffset.UtcNow;
                _taskRepository.Update(otherTask);
            }
        }

        await MoveNextAsync(instance, node, cancellationToken);
    }

    private async Task MoveNextAsync(
        WorkflowInstance instance,
        WorkflowNode currentNode,
        CancellationToken cancellationToken)
    {
        var guard = 0;
        var node = currentNode;

        while (guard++ < MaxNodeTransitions)
        {
            if (node.NodeType == WorkflowNodeType.End)
            {
                await CompleteInstanceAsync(instance, node, cancellationToken);
                return;
            }

            var nextNode = ResolveNextNode(instance, node);
            if (nextNode is null)
            {
                throw new BusinessException(ErrorCode.ValidationFailed, $"Workflow node '{node.NodeName}' cannot resolve next node.");
            }

            if (nextNode.NodeType == WorkflowNodeType.Approver)
            {
                await EnterApproverNodeAsync(instance, nextNode, cancellationToken);
                return;
            }

            if (nextNode.NodeType == WorkflowNodeType.Cc)
            {
                await EnterCcNodeAsync(instance, nextNode, cancellationToken);
                node = nextNode;
                continue;
            }

            if (nextNode.NodeType is WorkflowNodeType.Condition or WorkflowNodeType.Start)
            {
                node = nextNode;
                continue;
            }

            if (nextNode.NodeType == WorkflowNodeType.End)
            {
                node = nextNode;
                continue;
            }
        }

        throw new BusinessException(ErrorCode.ValidationFailed, "Workflow transition limit exceeded. Please check workflow branches.");
    }

    private WorkflowNode? ResolveNextNode(WorkflowInstance instance, WorkflowNode currentNode)
    {
        var edges = _edgeRepository.Query()
            .Where(entity => entity.DefinitionId == instance.DefinitionId && entity.FromNodeKey == currentNode.NodeKey)
            .OrderBy(entity => entity.Sort)
            .ToList();
        WorkflowEdge? selectedEdge = null;

        if (currentNode.NodeType == WorkflowNodeType.Condition)
        {
            var conditions = _conditionRepository.Query()
                .Where(entity => entity.DefinitionId == instance.DefinitionId)
                .ToDictionary(entity => entity.Id);

            selectedEdge = edges.FirstOrDefault(edge =>
                !edge.IsDefault &&
                edge.ConditionId.HasValue &&
                conditions.TryGetValue(edge.ConditionId.Value, out var condition) &&
                _conditionEvaluator.Evaluate(condition.ExpressionJson, instance.FormDataJson))
                ?? edges.FirstOrDefault(edge => edge.IsDefault);
        }
        else
        {
            selectedEdge = edges.FirstOrDefault();
        }

        return selectedEdge is null
            ? null
            : _nodeRepository.Query().FirstOrDefault(entity =>
                entity.DefinitionId == instance.DefinitionId &&
                entity.NodeKey == selectedEdge.ToNodeKey);
    }

    private async Task EnterApproverNodeAsync(
        WorkflowInstance instance,
        WorkflowNode node,
        CancellationToken cancellationToken)
    {
        var approverUserIds = _approverResolver.ResolveApproverUserIds(node, instance, instance.FormDataJson);
        if (approverUserIds.Count == 0)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, $"Approver node '{node.NodeName}' did not resolve any approvers.");
        }

        instance.CurrentNodeKey = node.NodeKey;
        _instanceRepository.Update(instance);

        var mode = node.ApprovalMode ?? WorkflowApprovalMode.Single;
        var taskUserIds = mode switch
        {
            WorkflowApprovalMode.Single => approverUserIds.Take(1).ToArray(),
            WorkflowApprovalMode.Sequential => approverUserIds.Take(1).ToArray(),
            _ => approverUserIds.ToArray()
        };
        var users = _userRepository.Query()
            .Where(entity => entity.TenantId == instance.TenantId && taskUserIds.Contains(entity.Id))
            .ToDictionary(entity => entity.Id);

        foreach (var userId in taskUserIds)
        {
            var user = users[userId];
            var task = await CreateTaskAsync(instance, node.NodeKey, node.NodeName, user.Id, user.DisplayName, cancellationToken);
            await NotifyTaskAsync(instance, task, "新的审批任务", $"流程“{instance.BusinessTitle}”需要你审批。", cancellationToken);
        }
    }

    private async Task EnterCcNodeAsync(
        WorkflowInstance instance,
        WorkflowNode node,
        CancellationToken cancellationToken)
    {
        var userIds = _approverResolver.ResolveApproverUserIds(node, instance, instance.FormDataJson);
        if (userIds.Count == 0)
        {
            return;
        }

        var users = _userRepository.Query()
            .Where(entity => entity.TenantId == instance.TenantId && userIds.Contains(entity.Id))
            .ToList();

        foreach (var user in users)
        {
            var cc = new WorkflowCc
            {
                Id = Guid.NewGuid(),
                TenantId = instance.TenantId,
                InstanceId = instance.Id,
                NodeKey = node.NodeKey,
                CcUserId = user.Id,
                CcUserName = user.DisplayName,
                IsRead = false
            };
            await _ccRepository.AddAsync(cc, cancellationToken);
            await AddRecordAsync(instance, null, node, WorkflowActionType.Cc, $"Cc to {user.DisplayName}.", cancellationToken);
            await NotifyUserAsync(instance, user.Id, "新的流程抄送", $"流程“{instance.BusinessTitle}”抄送给你。", cancellationToken);
        }
    }

    private async Task CompleteInstanceAsync(
        WorkflowInstance instance,
        WorkflowNode node,
        CancellationToken cancellationToken)
    {
        instance.Status = WorkflowInstanceStatus.Approved;
        instance.CurrentNodeKey = node.NodeKey;
        instance.CompletedAt = DateTimeOffset.UtcNow;
        _instanceRepository.Update(instance);

        await AddRecordAsync(instance, null, node, WorkflowActionType.Complete, "Workflow completed.", cancellationToken);
        await _businessHandlerResolver.Resolve(instance.BusinessType)
            .OnWorkflowApprovedAsync(BuildBusinessContext(instance, WorkflowActionType.Approve, "Workflow completed."), cancellationToken);
        await NotifyStarterAsync(instance, "流程已完成", $"流程“{instance.BusinessTitle}”已审批完成。", cancellationToken);
    }

    private async Task<WorkflowTask> CreateTaskAsync(
        WorkflowInstance instance,
        string nodeKey,
        string nodeName,
        Guid approverUserId,
        string approverUserName,
        CancellationToken cancellationToken)
    {
        var task = new WorkflowTask
        {
            Id = Guid.NewGuid(),
            TenantId = instance.TenantId,
            InstanceId = instance.Id,
            NodeKey = nodeKey,
            NodeName = nodeName,
            ApproverUserId = approverUserId,
            ApproverUserName = approverUserName,
            Status = WorkflowTaskStatus.Pending,
            AssignedAt = DateTimeOffset.UtcNow
        };

        await _taskRepository.AddAsync(task, cancellationToken);
        return task;
    }

    private async Task AddRecordAsync(
        WorkflowInstance instance,
        WorkflowTask? task,
        WorkflowNode? node,
        WorkflowActionType action,
        string? comment,
        CancellationToken cancellationToken)
    {
        await _recordRepository.AddAsync(new WorkflowRecord
        {
            Id = Guid.NewGuid(),
            TenantId = instance.TenantId,
            InstanceId = instance.Id,
            TaskId = task?.Id,
            NodeKey = node?.NodeKey ?? task?.NodeKey,
            NodeName = node?.NodeName ?? task?.NodeName,
            OperatorUserId = _currentUserService.UserId,
            OperatorUserName = _currentUserService.Username,
            Action = action,
            Comment = comment,
            OperatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private void ClosePendingTasks(Guid instanceId, Guid? exceptTaskId)
    {
        foreach (var task in _taskRepository.Query()
            .Where(entity => entity.InstanceId == instanceId &&
                entity.Status == WorkflowTaskStatus.Pending &&
                (!exceptTaskId.HasValue || entity.Id != exceptTaskId.Value))
            .ToList())
        {
            task.Status = WorkflowTaskStatus.Canceled;
            task.CompletedAt = DateTimeOffset.UtcNow;
            _taskRepository.Update(task);
        }
    }

    private WorkflowDefinition GetPublishedDefinitionByBusinessType(Guid tenantId, string businessType)
    {
        var binding = _bindingRepository.Query()
            .FirstOrDefault(entity => entity.TenantId == tenantId &&
                entity.BusinessType == businessType &&
                entity.IsEnabled)
            ?? throw new BusinessException(ErrorCode.NotFound, "Workflow business binding was not found.");

        return _definitionRepository.Query()
            .FirstOrDefault(entity => entity.TenantId == tenantId &&
                entity.Id == binding.DefinitionId &&
                entity.IsPublished &&
                entity.Status == WorkflowDefinitionStatus.Published)
            ?? throw new BusinessException(ErrorCode.NotFound, "Published workflow definition was not found.");
    }

    private static string? ResolveFormDataJson(StartWorkflowInstanceRequest request)
    {
        if (request.FormData.HasValue && request.FormData.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            return request.FormData.Value.GetRawText();
        }

        return string.IsNullOrWhiteSpace(request.FormDataJson) ? null : request.FormDataJson.Trim();
    }

    private static WorkflowBusinessContext BuildBusinessContext(
        WorkflowInstance instance,
        WorkflowActionType action,
        string? comment)
    {
        return new WorkflowBusinessContext
        {
            BusinessType = instance.BusinessType,
            BusinessId = instance.BusinessId,
            BusinessTitle = instance.BusinessTitle,
            WorkflowInstanceId = instance.Id,
            StarterUserId = instance.StarterUserId,
            StarterUserName = instance.StarterUserName,
            FormDataJson = instance.FormDataJson,
            Action = action,
            Comment = comment
        };
    }

    private WorkflowNode GetNodeOrThrow(Guid definitionId, string nodeKey)
    {
        return _nodeRepository.Query()
            .FirstOrDefault(entity => entity.DefinitionId == definitionId && entity.NodeKey == nodeKey)
            ?? throw new BusinessException(ErrorCode.NotFound, "Workflow node was not found.");
    }

    private async Task<WorkflowTask> GetPendingTaskForCurrentUserAsync(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Workflow task was not found.");

        if (task.ApproverUserId != userId)
        {
            throw new BusinessException(ErrorCode.Forbidden, "You are not allowed to handle this workflow task.");
        }

        if (task.Status != WorkflowTaskStatus.Pending)
        {
            throw new BusinessException(ErrorCode.Conflict, "Workflow task has already been handled.");
        }

        return task;
    }

    private async Task<WorkflowInstance> GetRunningInstanceOrThrowAsync(
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(instanceId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Workflow instance was not found.");

        if (instance.Status != WorkflowInstanceStatus.Running)
        {
            throw new BusinessException(ErrorCode.Conflict, "Workflow instance is not running.");
        }

        return instance;
    }

    private User GetEnabledUserOrThrow(Guid tenantId, Guid userId)
    {
        return _userRepository.Query()
            .FirstOrDefault(entity => entity.TenantId == tenantId && entity.Id == userId && entity.IsEnabled)
            ?? throw new BusinessException(ErrorCode.NotFound, "Target user was not found or disabled.");
    }

    private async Task NotifyTaskAsync(
        WorkflowInstance instance,
        WorkflowTask task,
        string title,
        string content,
        CancellationToken cancellationToken)
    {
        await NotifyUserAsync(instance, task.ApproverUserId, title, content, cancellationToken);
    }

    private async Task NotifyStarterAsync(
        WorkflowInstance instance,
        string title,
        string content,
        CancellationToken cancellationToken)
    {
        await NotifyUserAsync(instance, instance.StarterUserId, title, content, cancellationToken);
    }

    private async Task NotifyUserAsync(
        WorkflowInstance instance,
        Guid userId,
        string title,
        string content,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.SendSystemNotificationAsync(new SendSystemNotificationRequest
            {
                TenantId = instance.TenantId,
                RecipientUserIds = [userId],
                Type = NotificationTypes.Approval,
                Title = title,
                Content = content,
                LinkUrl = $"/workflow/instances/{instance.Id}",
                Payload = $"{{\"instanceId\":\"{instance.Id}\"}}"
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to enqueue workflow notification. InstanceId: {InstanceId}", instance.Id);
        }
    }

    private WorkflowInstanceDetailResponse BuildInstanceDetailResponse(WorkflowInstance instance)
    {
        var tasks = _taskRepository.Query()
            .Where(entity => entity.InstanceId == instance.Id)
            .OrderBy(entity => entity.AssignedAt)
            .ToList()
            .Select(task => new WorkflowTaskResponse
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
                BusinessType = instance.BusinessType,
                BusinessId = instance.BusinessId,
                BusinessTitle = instance.BusinessTitle,
                DefinitionName = instance.DefinitionName,
                StarterUserName = instance.StarterUserName,
                InstanceStatus = instance.Status,
                StartedAt = instance.StartedAt
            })
            .ToList();
        var records = _recordRepository.Query()
            .Where(entity => entity.InstanceId == instance.Id)
            .OrderBy(entity => entity.OperatedAt)
            .ToList()
            .Select(record => new WorkflowRecordResponse
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
            })
            .ToList();
        var ccs = _ccRepository.Query()
            .Where(entity => entity.InstanceId == instance.Id)
            .OrderBy(entity => entity.CreatedAt)
            .ToList()
            .Select(cc => new WorkflowCcResponse
            {
                Id = cc.Id,
                TenantId = cc.TenantId,
                InstanceId = cc.InstanceId,
                NodeKey = cc.NodeKey,
                CcUserId = cc.CcUserId,
                CcUserName = cc.CcUserName,
                IsRead = cc.IsRead,
                ReadAt = cc.ReadAt,
                BusinessType = instance.BusinessType,
                BusinessId = instance.BusinessId,
                BusinessTitle = instance.BusinessTitle,
                DefinitionName = instance.DefinitionName,
                StarterUserName = instance.StarterUserName,
                InstanceStatus = instance.Status,
                CreatedAt = cc.CreatedAt
            })
            .ToList();

        return new WorkflowInstanceDetailResponse
        {
            Id = instance.Id,
            TenantId = instance.TenantId,
            DefinitionId = instance.DefinitionId,
            DefinitionCode = instance.DefinitionCode,
            DefinitionName = instance.DefinitionName,
            BusinessType = instance.BusinessType,
            BusinessId = instance.BusinessId,
            BusinessTitle = instance.BusinessTitle,
            StarterUserId = instance.StarterUserId,
            StarterUserName = instance.StarterUserName,
            Status = instance.Status,
            CurrentNodeKey = instance.CurrentNodeKey,
            FormDataJson = instance.FormDataJson,
            StartedAt = instance.StartedAt,
            CompletedAt = instance.CompletedAt,
            CreatedAt = instance.CreatedAt,
            Tasks = tasks,
            Ccs = ccs,
            Records = records
        };
    }

    private Guid RequireUserId()
    {
        return _currentUserService.UserId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "User is not authenticated.");
    }

    private Guid RequireTenantId()
    {
        return _currentUserService.TenantId
            ?? throw new BusinessException(ErrorCode.ValidationFailed, "TenantId is required.");
    }

    private static string TrimRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }
}
