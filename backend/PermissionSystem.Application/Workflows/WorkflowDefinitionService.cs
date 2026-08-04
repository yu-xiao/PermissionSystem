using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Workflows;

public sealed class WorkflowDefinitionService : IWorkflowDefinitionService
{
    private readonly IRepository<WorkflowDefinition> _definitionRepository;
    private readonly IRepository<WorkflowNode> _nodeRepository;
    private readonly IRepository<WorkflowEdge> _edgeRepository;
    private readonly IRepository<WorkflowCondition> _conditionRepository;
    private readonly IRepository<WorkflowInstance> _instanceRepository;
    private readonly IRepository<WorkflowBusinessBinding> _bindingRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowDefinitionService(
        IRepository<WorkflowDefinition> definitionRepository,
        IRepository<WorkflowNode> nodeRepository,
        IRepository<WorkflowEdge> edgeRepository,
        IRepository<WorkflowCondition> conditionRepository,
        IRepository<WorkflowInstance> instanceRepository,
        IRepository<WorkflowBusinessBinding> bindingRepository,
        ICurrentUserService currentUserService,
        ITenantWriteResolver tenantWriteResolver,
        IUnitOfWork unitOfWork)
    {
        _definitionRepository = definitionRepository;
        _nodeRepository = nodeRepository;
        _edgeRepository = edgeRepository;
        _conditionRepository = conditionRepository;
        _instanceRepository = instanceRepository;
        _bindingRepository = bindingRepository;
        _currentUserService = currentUserService;
        _tenantWriteResolver = tenantWriteResolver;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<WorkflowDefinitionListResponse>> GetPagedAsync(
        WorkflowDefinitionQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _definitionRepository.Query();
        var tenantId = ResolveTenantId(request.TenantId);
        if (tenantId.HasValue)
        {
            query = query.Where(entity => entity.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.Code.Contains(keyword) ||
                entity.Name.Contains(keyword) ||
                (entity.Description != null && entity.Description.Contains(keyword)));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(entity => entity.Status == request.Status.Value);
        }

        if (request.IsPublished.HasValue)
        {
            query = query.Where(entity => entity.IsPublished == request.IsPublished.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderBy(entity => entity.Code)
            .ThenByDescending(entity => entity.Version)
            .ThenByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToListResponse)
            .ToList();

        return Task.FromResult(PagedResult<WorkflowDefinitionListResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<WorkflowDefinitionDetailResponse> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        return ToDetailResponse(definition, BuildDesignerResponse(definition.Id));
    }

    public async Task<WorkflowDefinitionListResponse> CreateAsync(
        CreateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveRequiredTenantId(request.TenantId);
        var code = TrimRequired(request.Code, "Workflow definition code is required.");
        var name = TrimRequired(request.Name, "Workflow definition name is required.");
        var businessType = NormalizeNullable(request.BusinessType);

        if (_definitionRepository.Query().Any(entity => entity.TenantId == tenantId && entity.Code == code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Workflow definition code already exists.");
        }

        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Description = request.Description,
            Version = 1,
            Status = WorkflowDefinitionStatus.Draft,
            IsPublished = false
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _definitionRepository.AddAsync(definition, token);
            await UpsertBusinessBindingAsync(definition, businessType, isEnabled: false, token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return ToListResponse(definition);
    }

    public async Task<WorkflowDefinitionListResponse> UpdateAsync(
        Guid id,
        UpdateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        var businessType = NormalizeNullable(request.BusinessType);

        definition.Name = TrimRequired(request.Name, "Workflow definition name is required.");
        definition.Description = request.Description;

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            _definitionRepository.Update(definition);
            await UpsertBusinessBindingAsync(definition, businessType, definition.IsPublished, token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return ToListResponse(definition);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        if (definition.IsPublished || definition.PublishedAt.HasValue || definition.Status == WorkflowDefinitionStatus.Published)
        {
            throw new BusinessException(ErrorCode.Conflict, "Published workflow definitions cannot be deleted. Disable them instead.");
        }

        if (_instanceRepository.Query().Any(entity => entity.DefinitionId == definition.Id))
        {
            throw new BusinessException(ErrorCode.Conflict, "Workflow definition has instances and cannot be deleted.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            foreach (var edge in _edgeRepository.Query().Where(entity => entity.DefinitionId == definition.Id).ToList())
            {
                _edgeRepository.Remove(edge);
            }

            foreach (var condition in _conditionRepository.Query().Where(entity => entity.DefinitionId == definition.Id).ToList())
            {
                _conditionRepository.Remove(condition);
            }

            foreach (var node in _nodeRepository.Query().Where(entity => entity.DefinitionId == definition.Id).ToList())
            {
                _nodeRepository.Remove(node);
            }

            foreach (var binding in _bindingRepository.Query().Where(entity => entity.DefinitionId == definition.Id).ToList())
            {
                _bindingRepository.Remove(binding);
            }

            _definitionRepository.Remove(definition);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public async Task<WorkflowDesignerResponse> GetDesignerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        return BuildDesignerResponse(definition.Id);
    }

    public async Task<WorkflowDesignerResponse> SaveDesignerAsync(
        Guid id,
        SaveWorkflowDesignerRequest request,
        CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        EnsureStructureCanBeModified(definition);
        ValidateDesignerRequest(request);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await SaveNodesAsync(definition, request.Nodes, token);
            await SaveConditionsAsync(definition, request.Conditions, token);
            await SaveEdgesAsync(definition, request.Edges, token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return BuildDesignerResponse(definition.Id);
    }

    public async Task<WorkflowDefinitionListResponse> PublishAsync(
        Guid id,
        PublishWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        ValidateDefinitionBeforePublish(definition.Id);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var sameCodeDefinitions = _definitionRepository.Query()
                .Where(entity => entity.TenantId == definition.TenantId &&
                    entity.Code == definition.Code &&
                    entity.Id != definition.Id &&
                    entity.IsPublished)
                .ToList();

            foreach (var otherDefinition in sameCodeDefinitions)
            {
                otherDefinition.IsPublished = false;
                otherDefinition.Status = WorkflowDefinitionStatus.Archived;
                _definitionRepository.Update(otherDefinition);

                foreach (var binding in _bindingRepository.Query().Where(entity => entity.DefinitionId == otherDefinition.Id && entity.IsEnabled).ToList())
                {
                    binding.IsEnabled = false;
                    _bindingRepository.Update(binding);
                }
            }

            definition.Status = WorkflowDefinitionStatus.Published;
            definition.IsPublished = true;
            definition.PublishedAt = DateTimeOffset.UtcNow;
            _definitionRepository.Update(definition);

            await EnablePublishedBusinessBindingAsync(definition, token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return ToListResponse(definition);
    }

    public async Task<WorkflowDefinitionListResponse> DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionOrThrowAsync(id, cancellationToken);
        if (!definition.IsPublished && definition.Status != WorkflowDefinitionStatus.Published)
        {
            throw new BusinessException(ErrorCode.Conflict, "Only published workflow definitions can be disabled.");
        }

        definition.Status = WorkflowDefinitionStatus.Disabled;
        definition.IsPublished = false;

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            _definitionRepository.Update(definition);
            foreach (var binding in _bindingRepository.Query().Where(entity => entity.DefinitionId == definition.Id && entity.IsEnabled).ToList())
            {
                binding.IsEnabled = false;
                _bindingRepository.Update(binding);
            }

            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return ToListResponse(definition);
    }

    public async Task<WorkflowDefinitionDetailResponse> CopyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var source = await GetDefinitionOrThrowAsync(id, cancellationToken);
        var nextVersion = _definitionRepository.Query()
            .Where(entity => entity.TenantId == source.TenantId && entity.Code == source.Code)
            .Select(entity => entity.Version)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var target = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = source.TenantId,
            Code = source.Code,
            Name = source.Name,
            Description = source.Description,
            Version = nextVersion,
            Status = WorkflowDefinitionStatus.Draft,
            IsPublished = false
        };
        var businessType = ResolveBusinessType(source);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _definitionRepository.AddAsync(target, token);
            await UpsertBusinessBindingAsync(target, businessType, isEnabled: false, token);
            await _unitOfWork.SaveChangesAsync(token);

            var sourceNodes = _nodeRepository.Query()
                .Where(entity => entity.DefinitionId == source.Id)
                .OrderBy(entity => entity.Sort)
                .ToList();
            var sourceConditions = _conditionRepository.Query()
                .Where(entity => entity.DefinitionId == source.Id)
                .OrderBy(entity => entity.Sort)
                .ToList();
            var sourceEdges = _edgeRepository.Query()
                .Where(entity => entity.DefinitionId == source.Id)
                .OrderBy(entity => entity.Sort)
                .ToList();
            var conditionIdMap = sourceConditions.ToDictionary(entity => entity.Id, _ => Guid.NewGuid());

            foreach (var node in sourceNodes)
            {
                await _nodeRepository.AddAsync(new WorkflowNode
                {
                    TenantId = target.TenantId,
                    DefinitionId = target.Id,
                    NodeKey = node.NodeKey,
                    NodeName = node.NodeName,
                    NodeType = node.NodeType,
                    ApproverType = node.ApproverType,
                    ApproverIds = node.ApproverIds,
                    ApprovalMode = node.ApprovalMode,
                    ConfigJson = node.ConfigJson,
                    PositionX = node.PositionX,
                    PositionY = node.PositionY,
                    Sort = node.Sort
                }, token);
            }

            foreach (var condition in sourceConditions)
            {
                await _conditionRepository.AddAsync(new WorkflowCondition
                {
                    Id = conditionIdMap[condition.Id],
                    TenantId = target.TenantId,
                    DefinitionId = target.Id,
                    NodeKey = condition.NodeKey,
                    ConditionName = condition.ConditionName,
                    ExpressionJson = condition.ExpressionJson,
                    Sort = condition.Sort
                }, token);
            }

            foreach (var edge in sourceEdges)
            {
                await _edgeRepository.AddAsync(new WorkflowEdge
                {
                    TenantId = target.TenantId,
                    DefinitionId = target.Id,
                    FromNodeKey = edge.FromNodeKey,
                    ToNodeKey = edge.ToNodeKey,
                    ConditionId = edge.ConditionId.HasValue && conditionIdMap.TryGetValue(edge.ConditionId.Value, out var newConditionId)
                        ? newConditionId
                        : null,
                    IsDefault = edge.IsDefault,
                    Sort = edge.Sort
                }, token);
            }

            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return ToDetailResponse(target, BuildDesignerResponse(target.Id));
    }

    private async Task UpsertBusinessBindingAsync(
        WorkflowDefinition definition,
        string? businessType,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(businessType))
        {
            foreach (var binding in _bindingRepository.Query().Where(entity => entity.DefinitionId == definition.Id).ToList())
            {
                _bindingRepository.Remove(binding);
            }

            return;
        }

        var normalizedBusinessType = businessType.Trim();
        var existing = _bindingRepository.Query()
            .FirstOrDefault(entity => entity.TenantId == definition.TenantId && entity.DefinitionId == definition.Id);
        if (existing is null)
        {
            existing = _bindingRepository.Query()
                .FirstOrDefault(entity => entity.TenantId == definition.TenantId &&
                    entity.BusinessType == normalizedBusinessType &&
                    entity.DefinitionId == definition.Id);
        }

        var conflictingBinding = _bindingRepository.Query()
            .FirstOrDefault(entity => entity.TenantId == definition.TenantId &&
                entity.BusinessType == normalizedBusinessType &&
                entity.DefinitionId != definition.Id);
        if (conflictingBinding is not null)
        {
            var conflictingDefinition = _definitionRepository.Query()
                .FirstOrDefault(entity => entity.Id == conflictingBinding.DefinitionId);
            if (conflictingDefinition is not null &&
                string.Equals(conflictingDefinition.Code, definition.Code, StringComparison.OrdinalIgnoreCase))
            {
                if (existing is not null)
                {
                    _bindingRepository.Remove(existing);
                }

                return;
            }

            throw new BusinessException(ErrorCode.Conflict, "Workflow business type is already bound to another definition.");
        }

        if (existing is null)
        {
            await _bindingRepository.AddAsync(new WorkflowBusinessBinding
            {
                TenantId = definition.TenantId,
                DefinitionId = definition.Id,
                BusinessType = normalizedBusinessType,
                BusinessName = definition.Name,
                DefinitionCode = definition.Code,
                DefinitionName = definition.Name,
                IsEnabled = isEnabled
            }, cancellationToken);
            return;
        }

        existing.BusinessType = normalizedBusinessType;
        existing.BusinessName = string.IsNullOrWhiteSpace(existing.BusinessName) ? definition.Name : existing.BusinessName;
        existing.DefinitionCode = definition.Code;
        existing.DefinitionName = definition.Name;
        existing.IsEnabled = isEnabled;
        _bindingRepository.Update(existing);
    }

    private async Task EnablePublishedBusinessBindingAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        var binding = _bindingRepository.Query()
            .FirstOrDefault(entity => entity.TenantId == definition.TenantId && entity.DefinitionId == definition.Id);
        var businessType = binding?.BusinessType ?? ResolveBusinessType(definition);
        if (string.IsNullOrWhiteSpace(businessType))
        {
            return;
        }

        var normalizedBusinessType = businessType.Trim();
        var bindings = _bindingRepository.Query()
            .Where(entity => entity.TenantId == definition.TenantId && entity.BusinessType == normalizedBusinessType)
            .ToList();

        if (binding is null)
        {
            binding = bindings.FirstOrDefault();
            if (binding is null)
            {
                binding = new WorkflowBusinessBinding
                {
                    TenantId = definition.TenantId,
                    BusinessType = normalizedBusinessType,
                    BusinessName = definition.Name,
                    DefinitionId = definition.Id
                };
                await _bindingRepository.AddAsync(binding, cancellationToken);
                bindings.Add(binding);
            }
            else
            {
                binding.DefinitionId = definition.Id;
            }
        }

        foreach (var item in bindings)
        {
            item.IsEnabled = item.Id == binding.Id;
            if (item.Id == binding.Id)
            {
                item.DefinitionId = definition.Id;
            }

            _bindingRepository.Update(item);
        }

        binding.BusinessType = normalizedBusinessType;
        binding.BusinessName = string.IsNullOrWhiteSpace(binding.BusinessName) ? definition.Name : binding.BusinessName;
        binding.DefinitionId = definition.Id;
        binding.DefinitionCode = definition.Code;
        binding.DefinitionName = definition.Name;
        binding.IsEnabled = true;
        _bindingRepository.Update(binding);
    }

    private string? ResolveBusinessType(WorkflowDefinition definition)
    {
        var direct = _bindingRepository.Query()
            .Where(entity => entity.TenantId == definition.TenantId && entity.DefinitionId == definition.Id)
            .OrderByDescending(entity => entity.IsEnabled)
            .ThenByDescending(entity => entity.CreatedAt)
            .Select(entity => entity.BusinessType)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        var relatedDefinitionIds = _definitionRepository.Query()
            .Where(entity => entity.TenantId == definition.TenantId && entity.Code == definition.Code)
            .Select(entity => entity.Id)
            .ToArray();

        return _bindingRepository.Query()
            .Where(entity => entity.TenantId == definition.TenantId && relatedDefinitionIds.Contains(entity.DefinitionId))
            .OrderByDescending(entity => entity.IsEnabled)
            .ThenByDescending(entity => entity.CreatedAt)
            .Select(entity => entity.BusinessType)
            .FirstOrDefault();
    }

    private async Task SaveNodesAsync(
        WorkflowDefinition definition,
        IReadOnlyCollection<WorkflowDesignerNodeRequest> requests,
        CancellationToken cancellationToken)
    {
        var existingNodes = _nodeRepository.Query()
            .Where(entity => entity.DefinitionId == definition.Id)
            .ToList();
        var existingById = existingNodes.ToDictionary(entity => entity.Id);
        var existingByKey = existingNodes.ToDictionary(entity => entity.NodeKey, StringComparer.OrdinalIgnoreCase);
        var keptIds = new HashSet<Guid>();

        foreach (var request in requests)
        {
            var node = ResolveExistingNode(request, existingById, existingByKey);
            if (node is null)
            {
                node = new WorkflowNode
                {
                    Id = request.Id.GetValueOrDefault(),
                    TenantId = definition.TenantId,
                    DefinitionId = definition.Id
                };
                await _nodeRepository.AddAsync(node, cancellationToken);
            }

            node.NodeKey = request.NodeKey.Trim();
            node.NodeName = request.NodeName.Trim();
            node.NodeType = request.NodeType;
            node.ApproverType = request.ApproverType;
            node.ApproverIds = NormalizeNullable(request.ApproverIds);
            node.ApprovalMode = request.ApprovalMode;
            node.ConfigJson = request.ConfigJson;
            node.PositionX = request.PositionX;
            node.PositionY = request.PositionY;
            node.Sort = request.Sort;

            if (node.Id != Guid.Empty)
            {
                keptIds.Add(node.Id);
            }
        }

        foreach (var node in existingNodes.Where(entity => !keptIds.Contains(entity.Id)))
        {
            _nodeRepository.Remove(node);
        }
    }

    private async Task SaveConditionsAsync(
        WorkflowDefinition definition,
        IReadOnlyCollection<WorkflowDesignerConditionRequest> requests,
        CancellationToken cancellationToken)
    {
        var existingConditions = _conditionRepository.Query()
            .Where(entity => entity.DefinitionId == definition.Id)
            .ToList();
        var existingById = existingConditions.ToDictionary(entity => entity.Id);
        var keptIds = new HashSet<Guid>();

        foreach (var request in requests)
        {
            WorkflowCondition? condition = null;
            if (request.Id.HasValue)
            {
                existingById.TryGetValue(request.Id.Value, out condition);
            }

            if (condition is null)
            {
                condition = new WorkflowCondition
                {
                    Id = request.Id.GetValueOrDefault(),
                    TenantId = definition.TenantId,
                    DefinitionId = definition.Id
                };
                await _conditionRepository.AddAsync(condition, cancellationToken);
            }

            condition.NodeKey = request.NodeKey.Trim();
            condition.ConditionName = request.ConditionName.Trim();
            condition.ExpressionJson = request.ExpressionJson.Trim();
            condition.Sort = request.Sort;

            if (condition.Id != Guid.Empty)
            {
                keptIds.Add(condition.Id);
            }
        }

        foreach (var condition in existingConditions.Where(entity => !keptIds.Contains(entity.Id)))
        {
            _conditionRepository.Remove(condition);
        }
    }

    private async Task SaveEdgesAsync(
        WorkflowDefinition definition,
        IReadOnlyCollection<WorkflowDesignerEdgeRequest> requests,
        CancellationToken cancellationToken)
    {
        var existingEdges = _edgeRepository.Query()
            .Where(entity => entity.DefinitionId == definition.Id)
            .ToList();
        var existingById = existingEdges.ToDictionary(entity => entity.Id);
        var keptIds = new HashSet<Guid>();

        foreach (var request in requests)
        {
            WorkflowEdge? edge = null;
            if (request.Id.HasValue)
            {
                existingById.TryGetValue(request.Id.Value, out edge);
            }

            if (edge is null)
            {
                edge = new WorkflowEdge
                {
                    Id = request.Id.GetValueOrDefault(),
                    TenantId = definition.TenantId,
                    DefinitionId = definition.Id
                };
                await _edgeRepository.AddAsync(edge, cancellationToken);
            }

            edge.FromNodeKey = request.FromNodeKey.Trim();
            edge.ToNodeKey = request.ToNodeKey.Trim();
            edge.ConditionId = request.ConditionId;
            edge.IsDefault = request.IsDefault;
            edge.Sort = request.Sort;

            if (edge.Id != Guid.Empty)
            {
                keptIds.Add(edge.Id);
            }
        }

        foreach (var edge in existingEdges.Where(entity => !keptIds.Contains(entity.Id)))
        {
            _edgeRepository.Remove(edge);
        }
    }

    private WorkflowDefinitionDesignerSnapshot GetDesignerSnapshot(Guid definitionId)
    {
        var nodes = _nodeRepository.Query()
            .Where(entity => entity.DefinitionId == definitionId)
            .OrderBy(entity => entity.Sort)
            .ToList();
        var edges = _edgeRepository.Query()
            .Where(entity => entity.DefinitionId == definitionId)
            .OrderBy(entity => entity.Sort)
            .ToList();
        var conditions = _conditionRepository.Query()
            .Where(entity => entity.DefinitionId == definitionId)
            .OrderBy(entity => entity.Sort)
            .ToList();

        return new WorkflowDefinitionDesignerSnapshot(nodes, edges, conditions);
    }

    private WorkflowDesignerResponse BuildDesignerResponse(Guid definitionId)
    {
        var snapshot = GetDesignerSnapshot(definitionId);
        return new WorkflowDesignerResponse
        {
            Nodes = snapshot.Nodes.Select(ToNodeResponse).ToList(),
            Edges = snapshot.Edges.Select(ToEdgeResponse).ToList(),
            Conditions = snapshot.Conditions.Select(ToConditionResponse).ToList()
        };
    }

    private void ValidateDefinitionBeforePublish(Guid definitionId)
    {
        var snapshot = GetDesignerSnapshot(definitionId);
        ValidateDesignerGraph(snapshot.Nodes, snapshot.Edges, snapshot.Conditions);
    }

    private static void ValidateDesignerRequest(SaveWorkflowDesignerRequest request)
    {
        var nodes = request.Nodes
            .Select(entity => new WorkflowNode
            {
                Id = entity.Id.GetValueOrDefault(),
                NodeKey = entity.NodeKey.Trim(),
                NodeName = entity.NodeName.Trim(),
                NodeType = entity.NodeType,
                ApproverType = entity.ApproverType,
                ApproverIds = NormalizeNullable(entity.ApproverIds),
                ApprovalMode = entity.ApprovalMode,
                Sort = entity.Sort
            })
            .ToList();
        var edges = request.Edges
            .Select(entity => new WorkflowEdge
            {
                Id = entity.Id.GetValueOrDefault(),
                FromNodeKey = entity.FromNodeKey.Trim(),
                ToNodeKey = entity.ToNodeKey.Trim(),
                ConditionId = entity.ConditionId,
                IsDefault = entity.IsDefault,
                Sort = entity.Sort
            })
            .ToList();
        var conditions = request.Conditions
            .Select(entity => new WorkflowCondition
            {
                Id = entity.Id.GetValueOrDefault(),
                NodeKey = entity.NodeKey.Trim(),
                ConditionName = entity.ConditionName.Trim(),
                ExpressionJson = entity.ExpressionJson.Trim(),
                Sort = entity.Sort
            })
            .ToList();

        ValidateDesignerGraph(nodes, edges, conditions);
    }

    private static void ValidateDesignerGraph(
        IReadOnlyCollection<WorkflowNode> nodes,
        IReadOnlyCollection<WorkflowEdge> edges,
        IReadOnlyCollection<WorkflowCondition> conditions)
    {
        if (nodes.Count == 0)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Workflow designer must contain nodes.");
        }

        if (nodes.Any(entity => string.IsNullOrWhiteSpace(entity.NodeKey)))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Workflow node key is required.");
        }

        if (nodes.Any(entity => string.IsNullOrWhiteSpace(entity.NodeName)))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Workflow node name is required.");
        }

        var duplicateNodeKey = nodes
            .GroupBy(entity => entity.NodeKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateNodeKey is not null)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, $"Workflow node key '{duplicateNodeKey.Key}' is duplicated.");
        }

        var startNodes = nodes.Where(entity => entity.NodeType == WorkflowNodeType.Start).ToList();
        if (startNodes.Count != 1)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Workflow definition must contain exactly one Start node.");
        }

        if (!nodes.Any(entity => entity.NodeType == WorkflowNodeType.End))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Workflow definition must contain at least one End node.");
        }

        foreach (var approverNode in nodes.Where(entity => entity.NodeType == WorkflowNodeType.Approver))
        {
            ValidateApproverNode(approverNode);
        }

        var nodeKeys = nodes.Select(entity => entity.NodeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges)
        {
            if (string.IsNullOrWhiteSpace(edge.FromNodeKey) || string.IsNullOrWhiteSpace(edge.ToNodeKey))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Workflow edge source and target are required.");
            }

            if (!nodeKeys.Contains(edge.FromNodeKey) || !nodeKeys.Contains(edge.ToNodeKey))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Workflow edge references an invalid node.");
            }
        }

        var conditionIds = conditions
            .Where(entity => entity.Id != Guid.Empty)
            .Select(entity => entity.Id)
            .ToHashSet();
        if (edges.Any(entity => entity.ConditionId.HasValue && !conditionIds.Contains(entity.ConditionId.Value)))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Workflow edge references an invalid condition.");
        }

        foreach (var condition in conditions)
        {
            if (!nodeKeys.Contains(condition.NodeKey))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Workflow condition references an invalid node.");
            }

            if (string.IsNullOrWhiteSpace(condition.ConditionName))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Workflow condition name is required.");
            }

            if (string.IsNullOrWhiteSpace(condition.ExpressionJson))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Workflow condition expression is required.");
            }
        }

        foreach (var conditionNode in nodes.Where(entity => entity.NodeType == WorkflowNodeType.Condition))
        {
            var outgoingEdges = edges.Where(entity => string.Equals(entity.FromNodeKey, conditionNode.NodeKey, StringComparison.OrdinalIgnoreCase)).ToList();
            if (outgoingEdges.Count == 0)
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Condition node must contain outgoing branches.");
            }

            if (!outgoingEdges.Any(entity => entity.IsDefault))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Condition node must contain a default branch.");
            }
        }

        EnsureGraphConnectivity(nodes, edges, startNodes[0].NodeKey);
        EnsureNoCycle(edges);
        EnsureEveryNodeCanReachEnd(nodes, edges);
    }

    private static void ValidateApproverNode(WorkflowNode node)
    {
        if (!node.ApproverType.HasValue)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Approver node must configure approver type.");
        }

        if (!node.ApprovalMode.HasValue)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Approver node must configure approval mode.");
        }

        var requiresApproverIds = node.ApproverType.Value is
            WorkflowApproverType.Users or
            WorkflowApproverType.Roles or
            WorkflowApproverType.DepartmentManager or
            WorkflowApproverType.Positions or
            WorkflowApproverType.FormFieldUser;
        if (requiresApproverIds && string.IsNullOrWhiteSpace(node.ApproverIds))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Approver node must configure approvers.");
        }
    }

    private static void EnsureGraphConnectivity(
        IReadOnlyCollection<WorkflowNode> nodes,
        IReadOnlyCollection<WorkflowEdge> edges,
        string startNodeKey)
    {
        var outgoing = edges
            .GroupBy(entity => entity.FromNodeKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ToNodeKey).ToList(), StringComparer.OrdinalIgnoreCase);
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(startNodeKey);

        while (stack.Count > 0)
        {
            var nodeKey = stack.Pop();
            if (!reachable.Add(nodeKey) || !outgoing.TryGetValue(nodeKey, out var nextNodeKeys))
            {
                continue;
            }

            foreach (var nextNodeKey in nextNodeKeys)
            {
                stack.Push(nextNodeKey);
            }
        }

        var unreachableNode = nodes.FirstOrDefault(entity => !reachable.Contains(entity.NodeKey));
        if (unreachableNode is not null)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, $"Workflow contains isolated node '{unreachableNode.NodeName}'.");
        }

        var nodeWithoutOutgoing = nodes.FirstOrDefault(entity =>
            entity.NodeType != WorkflowNodeType.End &&
            !outgoing.ContainsKey(entity.NodeKey));
        if (nodeWithoutOutgoing is not null)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, $"Workflow node '{nodeWithoutOutgoing.NodeName}' has no outgoing branch.");
        }
    }

    private static void EnsureNoCycle(IReadOnlyCollection<WorkflowEdge> edges)
    {
        var outgoing = edges
            .GroupBy(entity => entity.FromNodeKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ToNodeKey).ToList(), StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var nodeKey in outgoing.Keys)
        {
            if (HasCycle(nodeKey, outgoing, visited, visiting))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, "Workflow contains a cycle. Please remove loop branches before publishing.");
            }
        }
    }

    private static bool HasCycle(
        string nodeKey,
        IReadOnlyDictionary<string, List<string>> outgoing,
        HashSet<string> visited,
        HashSet<string> visiting)
    {
        if (visited.Contains(nodeKey))
        {
            return false;
        }

        if (!visiting.Add(nodeKey))
        {
            return true;
        }

        if (outgoing.TryGetValue(nodeKey, out var nextNodeKeys))
        {
            foreach (var nextNodeKey in nextNodeKeys)
            {
                if (HasCycle(nextNodeKey, outgoing, visited, visiting))
                {
                    return true;
                }
            }
        }

        visiting.Remove(nodeKey);
        visited.Add(nodeKey);
        return false;
    }

    private static void EnsureEveryNodeCanReachEnd(
        IReadOnlyCollection<WorkflowNode> nodes,
        IReadOnlyCollection<WorkflowEdge> edges)
    {
        var endNodeKeys = nodes
            .Where(entity => entity.NodeType == WorkflowNodeType.End)
            .Select(entity => entity.NodeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var outgoing = edges
            .GroupBy(entity => entity.FromNodeKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ToNodeKey).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (!CanReachEnd(node.NodeKey, outgoing, endNodeKeys, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            {
                throw new BusinessException(ErrorCode.ValidationFailed, $"Workflow node '{node.NodeName}' cannot reach an End node.");
            }
        }
    }

    private static bool CanReachEnd(
        string nodeKey,
        IReadOnlyDictionary<string, List<string>> outgoing,
        HashSet<string> endNodeKeys,
        HashSet<string> visited)
    {
        if (endNodeKeys.Contains(nodeKey))
        {
            return true;
        }

        if (!visited.Add(nodeKey) || !outgoing.TryGetValue(nodeKey, out var nextNodeKeys))
        {
            return false;
        }

        return nextNodeKeys.Any(nextNodeKey => CanReachEnd(nextNodeKey, outgoing, endNodeKeys, visited));
    }

    private async Task<WorkflowDefinition> GetDefinitionOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _definitionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Workflow definition was not found.");
    }

    private Guid? ResolveTenantId(Guid? requestedTenantId)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            return requestedTenantId;
        }

        return _currentUserService.TenantId ?? requestedTenantId;
    }

    private Guid ResolveRequiredTenantId(Guid? requestedTenantId)
    {
        return _tenantWriteResolver.ResolveTenantId(requestedTenantId);
    }

    private static void EnsureStructureCanBeModified(WorkflowDefinition definition)
    {
        if (definition.PublishedAt.HasValue || definition.Status == WorkflowDefinitionStatus.Published)
        {
            throw new BusinessException(ErrorCode.Conflict, "Published workflow definitions cannot be modified. Copy a new version first.");
        }
    }

    private static WorkflowNode? ResolveExistingNode(
        WorkflowDesignerNodeRequest request,
        IReadOnlyDictionary<Guid, WorkflowNode> existingById,
        IReadOnlyDictionary<string, WorkflowNode> existingByKey)
    {
        if (request.Id.HasValue && existingById.TryGetValue(request.Id.Value, out var nodeById))
        {
            return nodeById;
        }

        return !string.IsNullOrWhiteSpace(request.NodeKey) &&
            existingByKey.TryGetValue(request.NodeKey.Trim(), out var nodeByKey)
            ? nodeByKey
            : null;
    }

    private WorkflowDefinitionListResponse ToListResponse(WorkflowDefinition entity)
    {
        return new WorkflowDefinitionListResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            BusinessType = ResolveBusinessType(entity),
            Version = entity.Version,
            Status = entity.Status,
            IsPublished = entity.IsPublished,
            PublishedAt = entity.PublishedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private WorkflowDefinitionDetailResponse ToDetailResponse(
        WorkflowDefinition entity,
        WorkflowDesignerResponse designer)
    {
        return new WorkflowDefinitionDetailResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            BusinessType = ResolveBusinessType(entity),
            Version = entity.Version,
            Status = entity.Status,
            IsPublished = entity.IsPublished,
            PublishedAt = entity.PublishedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Designer = designer
        };
    }

    private static WorkflowDesignerNodeResponse ToNodeResponse(WorkflowNode entity)
    {
        return new WorkflowDesignerNodeResponse
        {
            Id = entity.Id,
            NodeKey = entity.NodeKey,
            NodeName = entity.NodeName,
            NodeType = entity.NodeType,
            ApproverType = entity.ApproverType,
            ApproverIds = entity.ApproverIds,
            ApprovalMode = entity.ApprovalMode,
            ConfigJson = entity.ConfigJson,
            PositionX = entity.PositionX,
            PositionY = entity.PositionY,
            Sort = entity.Sort
        };
    }

    private static WorkflowDesignerEdgeResponse ToEdgeResponse(WorkflowEdge entity)
    {
        return new WorkflowDesignerEdgeResponse
        {
            Id = entity.Id,
            FromNodeKey = entity.FromNodeKey,
            ToNodeKey = entity.ToNodeKey,
            ConditionId = entity.ConditionId,
            IsDefault = entity.IsDefault,
            Sort = entity.Sort
        };
    }

    private static WorkflowDesignerConditionResponse ToConditionResponse(WorkflowCondition entity)
    {
        return new WorkflowDesignerConditionResponse
        {
            Id = entity.Id,
            NodeKey = entity.NodeKey,
            ConditionName = entity.ConditionName,
            ExpressionJson = entity.ExpressionJson,
            Sort = entity.Sort
        };
    }

    private static string TrimRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record WorkflowDefinitionDesignerSnapshot(
        IReadOnlyCollection<WorkflowNode> Nodes,
        IReadOnlyCollection<WorkflowEdge> Edges,
        IReadOnlyCollection<WorkflowCondition> Conditions);
}
