using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Application.UserSessions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Tenants;

public sealed class TenantService : ITenantService
{
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<User> _userRepository;
    private readonly ITenantDirectoryRepository _tenantDirectoryRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IUnitOfWork _unitOfWork;

    public TenantService(
        IRepository<Tenant> tenantRepository,
        IRepository<User> userRepository,
        ITenantDirectoryRepository tenantDirectoryRepository,
        ICurrentUserService currentUserService,
        ITenantContext tenantContext,
        IPasswordHashService passwordHashService,
        IUserSessionService userSessionService,
        ITokenRevocationService tokenRevocationService,
        IScheduledTaskService scheduledTaskService,
        IBackgroundJobService backgroundJobService,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _tenantDirectoryRepository = tenantDirectoryRepository;
        _currentUserService = currentUserService;
        _tenantContext = tenantContext;
        _passwordHashService = passwordHashService;
        _userSessionService = userSessionService;
        _tokenRevocationService = tokenRevocationService;
        _scheduledTaskService = scheduledTaskService;
        _backgroundJobService = backgroundJobService;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<TenantResponse>> GetPagedAsync(
        TenantQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureSuperAdministrator();
        var query = _tenantDirectoryRepository.Query();

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
        else if (request.IsEnabled.HasValue)
        {
            query = request.IsEnabled.Value
                ? query.Where(entity => entity.Status == TenantStatus.Active)
                : query.Where(entity => entity.Status != TenantStatus.Active);
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

    public async Task<TenantResponse> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureSuperAdministrator();
        ValidateRequired(request.Code, "Tenant code is required.");
        ValidateRequired(request.Name, "Tenant name is required.");
        ValidateRequired(request.AdministratorUserName, "Administrator username is required.");
        ValidateRequired(request.AdministratorDisplayName, "Administrator display name is required.");
        ValidateBootstrapPassword(request.AdministratorPassword);

        var code = request.Code.Trim();
        if (_tenantDirectoryRepository.Query().Any(entity => entity.Code == code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Tenant code already exists.");
        }

        var id = Guid.NewGuid();
        SelectTargetTenant(id);
        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant
        {
            Id = id,
            TenantId = id,
            Code = code,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            Status = TenantStatus.Initializing,
            InitializationStep = "Queued",
            InitializationProgress = 0,
            StatusChangedAt = now
        };
        var administratorUserName = request.AdministratorUserName.Trim();
        var administrator = new User
        {
            TenantId = id,
            UserName = administratorUserName,
            NormalizedUserName = administratorUserName.ToUpperInvariant(),
            DisplayName = request.AdministratorDisplayName.Trim(),
            PasswordHash = _passwordHashService.HashPassword(request.AdministratorPassword),
            IsEnabled = true,
            IsBuiltin = true
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _tenantRepository.AddAsync(tenant, token);
            await _userRepository.AddAsync(administrator, token);
            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        await EnqueueInitializationAsync(tenant, cancellationToken);
        return ToResponse(tenant);
    }

    public async Task<TenantResponse> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureSuperAdministrator();
        ValidateRequired(request.Name, "Tenant name is required.");

        var tenant = await GetTenantOrThrowAsync(id, cancellationToken);
        SelectTargetTenant(tenant.Id);
        tenant.Name = request.Name.Trim();
        tenant.Description = NormalizeOptional(request.Description);

        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(tenant);
    }

    public Task SetEnabledAsync(
        Guid id,
        SetTenantEnabledRequest request,
        CancellationToken cancellationToken = default)
    {
        return request.IsEnabled
            ? RestoreAsync(id, cancellationToken)
            : DisableAsync(id, cancellationToken);
    }

    public async Task RetryInitializationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdministrator();
        var tenant = await GetTenantOrThrowAsync(id, cancellationToken);
        if (tenant.Status is not (TenantStatus.Failed or TenantStatus.Initializing))
        {
            throw new BusinessException(ErrorCode.Conflict, "Only failed or initializing tenants can retry initialization.");
        }

        SelectTargetTenant(tenant.Id);
        tenant.Status = TenantStatus.Initializing;
        tenant.StatusChangedAt = DateTimeOffset.UtcNow;
        tenant.InitializationStep = "Queued";
        tenant.InitializationError = null;
        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnqueueInitializationAsync(tenant, cancellationToken);
    }

    public async Task DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdministrator();
        var tenant = await GetTenantOrThrowAsync(id, cancellationToken);
        if (string.Equals(tenant.Code, SystemBuiltinConstants.DefaultTenantCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ErrorCode.Forbidden, "The default platform tenant cannot be disabled.");
        }
        if (tenant.Status is not (TenantStatus.Active or TenantStatus.Disabled))
        {
            throw new BusinessException(ErrorCode.Conflict, "Only active tenants can be disabled.");
        }

        SelectTargetTenant(tenant.Id);
        if (tenant.Status == TenantStatus.Active)
        {
            tenant.Status = TenantStatus.Disabled;
            tenant.StatusChangedAt = DateTimeOffset.UtcNow;
            _tenantRepository.Update(tenant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var userIds = _userRepository.QueryForTenant(tenant.Id).Select(entity => entity.Id).ToArray();
        await _userSessionService.RevokeTenantSessionsAsync(tenant.Id, "Tenant disabled.", cancellationToken);
        await _tokenRevocationService.RevokeUsersRefreshTokensAsync(userIds, cancellationToken);
        await _scheduledTaskService.SuspendTenantAsync(tenant.Id, cancellationToken);
    }

    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdministrator();
        var tenant = await GetTenantOrThrowAsync(id, cancellationToken);
        if (tenant.Status is not (TenantStatus.Active or TenantStatus.Disabled))
        {
            throw new BusinessException(ErrorCode.Conflict, "Only disabled tenants can be restored.");
        }

        SelectTargetTenant(tenant.Id);
        if (tenant.Status == TenantStatus.Disabled)
        {
            tenant.Status = TenantStatus.Active;
            tenant.StatusChangedAt = DateTimeOffset.UtcNow;
            _tenantRepository.Update(tenant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        await _scheduledTaskService.ResumeTenantAsync(tenant.Id, cancellationToken);
    }

    private async Task EnqueueInitializationAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        try
        {
            tenant.InitializationJobId = _backgroundJobService.Enqueue<TenantInitializationJob>(
                job => job.ExecuteAsync(tenant.Id));
            _tenantRepository.Update(tenant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            tenant.Status = TenantStatus.Failed;
            tenant.StatusChangedAt = DateTimeOffset.UtcNow;
            tenant.InitializationStep = "Enqueue";
            tenant.InitializationError = Truncate(exception.Message, 2000);
            _tenantRepository.Update(tenant);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task<Tenant> GetTenantOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _tenantDirectoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Tenant was not found.");
    }

    private void EnsureSuperAdministrator()
    {
        if (!_currentUserService.IsSuperAdmin)
        {
            throw new BusinessException(ErrorCode.Forbidden, "Only super administrators can manage tenants.");
        }
    }

    private void SelectTargetTenant(Guid tenantId) => _tenantContext.SetTenant(tenantId, "Request");

    private static TenantResponse ToResponse(Tenant tenant)
    {
        return new TenantResponse
        {
            Id = tenant.Id,
            TenantId = tenant.TenantId,
            Code = tenant.Code,
            Name = tenant.Name,
            Description = tenant.Description,
            IsEnabled = tenant.Status == TenantStatus.Active,
            Status = tenant.Status,
            InitializationStep = tenant.InitializationStep,
            InitializationProgress = tenant.InitializationProgress,
            InitializationAttempts = tenant.InitializationAttempts,
            InitializationError = tenant.InitializationError,
            InitializationStartedAt = tenant.InitializationStartedAt,
            InitializedAt = tenant.InitializedAt,
            StatusChangedAt = tenant.StatusChangedAt,
            CreatedAt = tenant.CreatedAt
        };
    }

    private static void ValidateBootstrapPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || !password.Any(char.IsDigit) || !password.Any(char.IsLower))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Administrator password must be at least 8 characters and contain a lowercase letter and a digit.");
        }
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
