using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Workflows;

public sealed class WorkflowBusinessBindingService : IWorkflowBusinessBindingService
{
    private readonly IRepository<WorkflowBusinessBinding> _bindingRepository;
    private readonly IRepository<WorkflowDefinition> _definitionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowBusinessBindingService(
        IRepository<WorkflowBusinessBinding> bindingRepository,
        IRepository<WorkflowDefinition> definitionRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _bindingRepository = bindingRepository;
        _definitionRepository = definitionRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<WorkflowBusinessBindingResponse>> GetPagedAsync(
        WorkflowBusinessBindingQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _bindingRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.BusinessType.Contains(keyword) ||
                entity.BusinessName.Contains(keyword) ||
                entity.DefinitionCode.Contains(keyword) ||
                entity.DefinitionName.Contains(keyword) ||
                (entity.Remark != null && entity.Remark.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessType))
        {
            var businessType = request.BusinessType.Trim();
            query = query.Where(entity => entity.BusinessType == businessType);
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var rows = query
            .OrderBy(entity => entity.BusinessType)
            .ThenByDescending(entity => entity.IsEnabled)
            .ThenByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList();
        var definitions = LoadDefinitions(rows.Select(entity => entity.DefinitionId));
        var items = rows
            .Select(entity => ToResponse(entity, definitions.GetValueOrDefault(entity.DefinitionId)))
            .ToList();

        return Task.FromResult(PagedResult<WorkflowBusinessBindingResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<WorkflowBusinessBindingResponse> CreateAsync(
        CreateWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveRequiredTenantId(request.TenantId);
        var businessType = TrimRequired(request.BusinessType, "Business type is required.");
        var businessName = TrimRequired(request.BusinessName, "Business name is required.");
        var definition = GetPublishedDefinitionOrThrow(tenantId, request.DefinitionId);

        if (_bindingRepository.Query().Any(entity => entity.TenantId == tenantId && entity.BusinessType == businessType))
        {
            throw new BusinessException(ErrorCode.Conflict, "Business type already has a workflow binding.");
        }

        var binding = new WorkflowBusinessBinding
        {
            TenantId = tenantId,
            BusinessType = businessType,
            BusinessName = businessName,
            DefinitionId = definition.Id,
            DefinitionCode = definition.Code,
            DefinitionName = definition.Name,
            IsEnabled = false,
            Remark = NormalizeNullable(request.Remark)
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _bindingRepository.AddAsync(binding, token);
            if (request.IsEnabled)
            {
                DisableOtherBindings(tenantId, businessType, binding.Id);
                binding.IsEnabled = true;
                _bindingRepository.Update(binding);
            }

            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return ToResponse(binding, definition);
    }

    public async Task<WorkflowBusinessBindingResponse> UpdateAsync(
        Guid id,
        UpdateWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        var binding = await GetBindingOrThrowAsync(id, cancellationToken);
        var businessType = TrimRequired(request.BusinessType, "Business type is required.");
        var businessName = TrimRequired(request.BusinessName, "Business name is required.");
        var definition = GetPublishedDefinitionOrThrow(binding.TenantId, request.DefinitionId);

        if (_bindingRepository.Query().Any(entity =>
            entity.TenantId == binding.TenantId &&
            entity.BusinessType == businessType &&
            entity.Id != binding.Id))
        {
            throw new BusinessException(ErrorCode.Conflict, "Business type already has a workflow binding.");
        }

        binding.BusinessType = businessType;
        binding.BusinessName = businessName;
        binding.DefinitionId = definition.Id;
        binding.DefinitionCode = definition.Code;
        binding.DefinitionName = definition.Name;
        binding.Remark = NormalizeNullable(request.Remark);

        _bindingRepository.Update(binding);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(binding, definition);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var binding = await GetBindingOrThrowAsync(id, cancellationToken);
        _bindingRepository.Remove(binding);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowBusinessBindingResponse> EnableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var binding = await GetBindingOrThrowAsync(id, cancellationToken);
        var definition = GetPublishedDefinitionOrThrow(binding.TenantId, binding.DefinitionId);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            DisableOtherBindings(binding.TenantId, binding.BusinessType, binding.Id);
            binding.DefinitionCode = definition.Code;
            binding.DefinitionName = definition.Name;
            binding.IsEnabled = true;
            _bindingRepository.Update(binding);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return ToResponse(binding, definition);
    }

    public async Task<WorkflowBusinessBindingResponse> DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var binding = await GetBindingOrThrowAsync(id, cancellationToken);
        binding.IsEnabled = false;
        _bindingRepository.Update(binding);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(binding, GetDefinitionOrThrow(binding.TenantId, binding.DefinitionId));
    }

    public Task<WorkflowBusinessBindingResponse> GetEnabledByBusinessTypeAsync(
        string businessType,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveRequiredTenantId(null);
        var normalizedBusinessType = TrimRequired(businessType, "Business type is required.");
        var binding = _bindingRepository.Query()
            .FirstOrDefault(entity => entity.TenantId == tenantId &&
                entity.BusinessType == normalizedBusinessType &&
                entity.IsEnabled)
            ?? throw new BusinessException(ErrorCode.NotFound, "Enabled workflow business binding was not found.");
        var definition = GetPublishedDefinitionOrThrow(binding.TenantId, binding.DefinitionId);

        return Task.FromResult(ToResponse(binding, definition));
    }

    private async Task<WorkflowBusinessBinding> GetBindingOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _bindingRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Workflow business binding was not found.");
    }

    private WorkflowDefinition GetPublishedDefinitionOrThrow(Guid tenantId, Guid definitionId)
    {
        var definition = GetDefinitionOrThrow(tenantId, definitionId);
        if (!definition.IsPublished || definition.Status != WorkflowDefinitionStatus.Published)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Workflow definition must be published before binding.");
        }

        return definition;
    }

    private WorkflowDefinition GetDefinitionOrThrow(Guid tenantId, Guid definitionId)
    {
        return _definitionRepository.Query()
            .FirstOrDefault(entity => entity.TenantId == tenantId && entity.Id == definitionId)
            ?? throw new BusinessException(ErrorCode.NotFound, "Workflow definition was not found.");
    }

    private void DisableOtherBindings(Guid tenantId, string businessType, Guid exceptBindingId)
    {
        foreach (var other in _bindingRepository.Query()
            .Where(entity => entity.TenantId == tenantId &&
                entity.BusinessType == businessType &&
                entity.Id != exceptBindingId &&
                entity.IsEnabled)
            .ToList())
        {
            other.IsEnabled = false;
            _bindingRepository.Update(other);
        }
    }

    private Dictionary<Guid, WorkflowDefinition> LoadDefinitions(IEnumerable<Guid> definitionIds)
    {
        var ids = definitionIds.Distinct().ToArray();
        return _definitionRepository.Query()
            .Where(entity => ids.Contains(entity.Id))
            .ToDictionary(entity => entity.Id);
    }

    private Guid? ResolveTenantId(Guid? requestedTenantId)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            return requestedTenantId ?? _currentUserService.TenantId;
        }

        return _currentUserService.TenantId ?? requestedTenantId;
    }

    private Guid ResolveRequiredTenantId(Guid? requestedTenantId)
    {
        var tenantId = ResolveTenantId(requestedTenantId);
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            throw new BusinessException(ErrorCode.BadRequest, "Tenant is required.");
        }

        return tenantId.Value;
    }

    private static WorkflowBusinessBindingResponse ToResponse(WorkflowBusinessBinding entity, WorkflowDefinition? definition)
    {
        return new WorkflowBusinessBindingResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            BusinessType = entity.BusinessType,
            BusinessName = entity.BusinessName,
            DefinitionId = entity.DefinitionId,
            DefinitionCode = definition?.Code ?? entity.DefinitionCode,
            DefinitionName = definition?.Name ?? entity.DefinitionName,
            DefinitionVersion = definition?.Version ?? 0,
            DefinitionStatus = definition?.Status ?? WorkflowDefinitionStatus.Disabled,
            IsEnabled = entity.IsEnabled,
            Remark = entity.Remark,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static string TrimRequired(string? value, string message)
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
}
