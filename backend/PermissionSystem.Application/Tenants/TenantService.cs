using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Tenants;

public sealed class TenantService : ITenantService
{
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly ITenantDirectoryRepository _tenantDirectoryRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public TenantService(
        IRepository<Tenant> tenantRepository,
        ITenantDirectoryRepository tenantDirectoryRepository,
        ICurrentUserService currentUserService,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _tenantDirectoryRepository = tenantDirectoryRepository;
        _currentUserService = currentUserService;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<TenantResponse>> GetPagedAsync(
        TenantQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = QueryTenants();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.Code.Contains(keyword) ||
                entity.Name.Contains(keyword) ||
                (entity.Description != null && entity.Description.Contains(keyword)));
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<TenantResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Code, "Tenant code is required.");
        ValidateRequired(request.Name, "Tenant name is required.");

        var code = request.Code.Trim();
        if (QueryTenants().Any(entity => entity.Code == code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Tenant code already exists.");
        }

        var id = Guid.NewGuid();
        SelectTargetTenantForSuperAdministrator(id);
        var tenant = new Tenant
        {
            Id = id,
            TenantId = id,
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description,
            IsEnabled = request.IsEnabled
        };

        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(tenant);
    }

    public async Task<TenantResponse> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Name, "Tenant name is required.");

        var tenant = await GetTenantOrThrowAsync(id, cancellationToken);
        SelectTargetTenantForSuperAdministrator(tenant.Id);
        tenant.Name = request.Name.Trim();
        tenant.Description = request.Description;
        tenant.IsEnabled = request.IsEnabled;

        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(tenant);
    }

    public async Task SetEnabledAsync(
        Guid id,
        SetTenantEnabledRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantOrThrowAsync(id, cancellationToken);
        SelectTargetTenantForSuperAdministrator(tenant.Id);
        tenant.IsEnabled = request.IsEnabled;

        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Tenant> GetTenantOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenant = _currentUserService.IsSuperAdmin
            ? await _tenantDirectoryRepository.GetByIdAsync(id, cancellationToken)
            : await _tenantRepository.GetByIdAsync(id, cancellationToken);

        return tenant
            ?? throw new BusinessException(ErrorCode.NotFound, "Tenant was not found.");
    }

    private IQueryable<Tenant> QueryTenants()
    {
        return _currentUserService.IsSuperAdmin
            ? _tenantDirectoryRepository.Query()
            : _tenantRepository.Query();
    }

    private void SelectTargetTenantForSuperAdministrator(Guid tenantId)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            _tenantContext.SetTenant(tenantId, "Request");
        }
    }

    private static TenantResponse ToResponse(Tenant tenant)
    {
        return new TenantResponse
        {
            Id = tenant.Id,
            TenantId = tenant.TenantId,
            Code = tenant.Code,
            Name = tenant.Name,
            Description = tenant.Description,
            IsEnabled = tenant.IsEnabled,
            CreatedAt = tenant.CreatedAt
        };
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }
}
