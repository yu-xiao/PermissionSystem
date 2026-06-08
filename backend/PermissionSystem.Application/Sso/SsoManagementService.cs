using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Sso;

public sealed class SsoManagementService : ISsoManagementService
{
    private readonly IRepository<SsoProvider> _providerRepository;
    private readonly IRepository<SsoUserBinding> _bindingRepository;
    private readonly IRepository<SsoRoleMapping> _roleMappingRepository;
    private readonly IRepository<SsoDepartmentMapping> _departmentMappingRepository;
    private readonly IRepository<SsoLoginLog> _loginLogRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SsoManagementService(
        IRepository<SsoProvider> providerRepository,
        IRepository<SsoUserBinding> bindingRepository,
        IRepository<SsoRoleMapping> roleMappingRepository,
        IRepository<SsoDepartmentMapping> departmentMappingRepository,
        IRepository<SsoLoginLog> loginLogRepository,
        IRepository<User> userRepository,
        IRepository<Role> roleRepository,
        IRepository<Department> departmentRepository,
        IUnitOfWork unitOfWork)
    {
        _providerRepository = providerRepository;
        _bindingRepository = bindingRepository;
        _roleMappingRepository = roleMappingRepository;
        _departmentMappingRepository = departmentMappingRepository;
        _loginLogRepository = loginLogRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<SsoUserBindingResponse>> GetUserBindingsAsync(
        SsoUserBindingQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var providersById = _providerRepository.Query().ToDictionary(entity => entity.Id);
        var usersById = _userRepository.Query().ToDictionary(entity => entity.Id);
        var query = _bindingRepository.Query();
        if (request.ProviderId.HasValue)
        {
            query = query.Where(entity => entity.ProviderId == request.ProviderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.ProviderCode.Contains(keyword) ||
                entity.ExternalUserId.Contains(keyword) ||
                (entity.ExternalUserName != null && entity.ExternalUserName.Contains(keyword)) ||
                (entity.ExternalEmail != null && entity.ExternalEmail.Contains(keyword)) ||
                (entity.ExternalPhone != null && entity.ExternalPhone.Contains(keyword)));
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(entity => ToBindingResponse(entity, providersById, usersById))
            .ToList();

        return Task.FromResult(PagedResult<SsoUserBindingResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<SsoUserBindingDetailResponse> GetUserBindingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var binding = await GetBindingOrThrowAsync(id, cancellationToken);
        var providersById = _providerRepository.Query().ToDictionary(entity => entity.Id);
        var usersById = _userRepository.Query().ToDictionary(entity => entity.Id);
        var response = ToBindingResponse(binding, providersById, usersById);
        return new SsoUserBindingDetailResponse
        {
            Id = response.Id,
            TenantId = response.TenantId,
            ProviderId = response.ProviderId,
            ProviderCode = response.ProviderCode,
            ProviderName = response.ProviderName,
            ExternalUserId = response.ExternalUserId,
            ExternalUserName = response.ExternalUserName,
            ExternalEmail = response.ExternalEmail,
            ExternalPhone = response.ExternalPhone,
            LocalUserId = response.LocalUserId,
            LocalUserName = response.LocalUserName,
            LocalDisplayName = response.LocalDisplayName,
            LastLoginAt = response.LastLoginAt,
            CreatedAt = response.CreatedAt,
            ClaimsJson = binding.ClaimsJson
        };
    }

    public async Task DeleteUserBindingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var binding = await GetBindingOrThrowAsync(id, cancellationToken);
        _bindingRepository.Remove(binding);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SsoRoleMappingResponse>> GetRoleMappingsAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(providerId, cancellationToken);
        var rolesById = _roleRepository.Query()
            .Where(entity => entity.TenantId == provider.TenantId)
            .ToDictionary(entity => entity.Id);
        var items = _roleMappingRepository.Query()
            .Where(entity => entity.ProviderId == providerId)
            .OrderBy(entity => entity.ExternalRole)
            .ToList()
            .Select(entity => ToRoleMappingResponse(entity, rolesById))
            .ToList();

        return items;
    }

    public async Task<IReadOnlyList<SsoRoleMappingResponse>> SaveRoleMappingsAsync(
        Guid providerId,
        IReadOnlyCollection<SsoRoleMappingRequest> request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(providerId, cancellationToken);
        var normalizedItems = request
            .Select(item => new SsoRoleMappingRequest
            {
                ExternalRole = TrimRequired(item.ExternalRole, "External role is required."),
                LocalRoleId = item.LocalRoleId
            })
            .GroupBy(item => new { ExternalRole = item.ExternalRole.ToUpperInvariant(), item.LocalRoleId })
            .Select(group => group.First())
            .ToArray();
        var roleIds = normalizedItems.Select(entity => entity.LocalRoleId).Distinct().ToArray();
        var roles = _roleRepository.Query()
            .Where(entity => entity.TenantId == provider.TenantId && roleIds.Contains(entity.Id) && entity.IsEnabled)
            .ToList();
        if (roles.Count != roleIds.Length)
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more local roles are invalid.");
        }

        if (roles.Any(IsSuperAdminRole))
        {
            throw new BusinessException(ErrorCode.Forbidden, "SSO role mapping cannot assign SuperAdmin automatically.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            foreach (var existing in _roleMappingRepository.Query().Where(entity => entity.ProviderId == providerId).ToList())
            {
                _roleMappingRepository.Remove(existing);
            }

            foreach (var item in normalizedItems)
            {
                await _roleMappingRepository.AddAsync(new SsoRoleMapping
                {
                    TenantId = provider.TenantId,
                    ProviderId = provider.Id,
                    ExternalRole = item.ExternalRole,
                    LocalRoleId = item.LocalRoleId
                }, token);
            }

            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return await GetRoleMappingsAsync(providerId, cancellationToken);
    }

    public async Task<IReadOnlyList<SsoDepartmentMappingResponse>> GetDepartmentMappingsAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(providerId, cancellationToken);
        var departmentsById = _departmentRepository.Query()
            .Where(entity => entity.TenantId == provider.TenantId)
            .ToDictionary(entity => entity.Id);
        var items = _departmentMappingRepository.Query()
            .Where(entity => entity.ProviderId == providerId)
            .OrderBy(entity => entity.ExternalDepartment)
            .ToList()
            .Select(entity => ToDepartmentMappingResponse(entity, departmentsById))
            .ToList();

        return items;
    }

    public async Task<IReadOnlyList<SsoDepartmentMappingResponse>> SaveDepartmentMappingsAsync(
        Guid providerId,
        IReadOnlyCollection<SsoDepartmentMappingRequest> request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(providerId, cancellationToken);
        var normalizedItems = request
            .Select(item => new SsoDepartmentMappingRequest
            {
                ExternalDepartment = TrimRequired(item.ExternalDepartment, "External department is required."),
                LocalDepartmentId = item.LocalDepartmentId
            })
            .GroupBy(item => new { ExternalDepartment = item.ExternalDepartment.ToUpperInvariant(), item.LocalDepartmentId })
            .Select(group => group.First())
            .ToArray();
        var departmentIds = normalizedItems.Select(entity => entity.LocalDepartmentId).Distinct().ToArray();
        var departments = _departmentRepository.Query()
            .Where(entity => entity.TenantId == provider.TenantId && departmentIds.Contains(entity.Id) && entity.IsEnabled)
            .ToList();
        if (departments.Count != departmentIds.Length)
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more local departments are invalid.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            foreach (var existing in _departmentMappingRepository.Query().Where(entity => entity.ProviderId == providerId).ToList())
            {
                _departmentMappingRepository.Remove(existing);
            }

            foreach (var item in normalizedItems)
            {
                await _departmentMappingRepository.AddAsync(new SsoDepartmentMapping
                {
                    TenantId = provider.TenantId,
                    ProviderId = provider.Id,
                    ExternalDepartment = item.ExternalDepartment,
                    LocalDepartmentId = item.LocalDepartmentId
                }, token);
            }

            await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return await GetDepartmentMappingsAsync(providerId, cancellationToken);
    }

    public Task<PagedResult<SsoLoginLogResponse>> GetLoginLogsAsync(
        SsoLoginLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyLoginLogQuery(_loginLogRepository.Query(), request);
        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToLoginLogResponse)
            .ToList();

        return Task.FromResult(PagedResult<SsoLoginLogResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<SsoLoginLogResponse> GetLoginLogAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ToLoginLogResponse(await _loginLogRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "SSO login log was not found."));
    }

    private IQueryable<SsoLoginLog> ApplyLoginLogQuery(IQueryable<SsoLoginLog> query, SsoLoginLogQueryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ProviderCode))
        {
            var providerCode = request.ProviderCode.Trim().ToUpperInvariant();
            query = query.Where(entity => entity.ProviderCode == providerCode);
        }

        if (request.ProviderType.HasValue)
        {
            query = query.Where(entity => entity.ProviderType == request.ProviderType.Value);
        }

        if (request.LoginResult.HasValue)
        {
            query = query.Where(entity => entity.LoginResult == request.LoginResult.Value);
        }

        if (request.StartAt.HasValue)
        {
            query = query.Where(entity => entity.CreatedAt >= request.StartAt.Value);
        }

        if (request.EndAt.HasValue)
        {
            query = query.Where(entity => entity.CreatedAt <= request.EndAt.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                (entity.ExternalUserId != null && entity.ExternalUserId.Contains(keyword)) ||
                (entity.ExternalUserName != null && entity.ExternalUserName.Contains(keyword)) ||
                (entity.LocalUserName != null && entity.LocalUserName.Contains(keyword)) ||
                (entity.IpAddress != null && entity.IpAddress.Contains(keyword)) ||
                (entity.TraceId != null && entity.TraceId.Contains(keyword)));
        }

        return query;
    }

    private async Task<SsoUserBinding> GetBindingOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _bindingRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "SSO user binding was not found.");
    }

    private async Task<SsoProvider> GetProviderOrThrowAsync(Guid providerId, CancellationToken cancellationToken)
    {
        return await _providerRepository.GetByIdAsync(providerId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "SSO provider was not found.");
    }

    private static SsoUserBindingResponse ToBindingResponse(
        SsoUserBinding entity,
        IReadOnlyDictionary<Guid, SsoProvider> providersById,
        IReadOnlyDictionary<Guid, User> usersById)
    {
        providersById.TryGetValue(entity.ProviderId, out var provider);
        usersById.TryGetValue(entity.LocalUserId, out var user);
        return new SsoUserBindingResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ProviderId = entity.ProviderId,
            ProviderCode = entity.ProviderCode,
            ProviderName = provider?.ProviderName,
            ExternalUserId = entity.ExternalUserId,
            ExternalUserName = entity.ExternalUserName,
            ExternalEmail = entity.ExternalEmail,
            ExternalPhone = entity.ExternalPhone,
            LocalUserId = entity.LocalUserId,
            LocalUserName = user?.UserName,
            LocalDisplayName = user?.DisplayName,
            LastLoginAt = entity.LastLoginAt,
            CreatedAt = entity.CreatedAt
        };
    }

    private static SsoRoleMappingResponse ToRoleMappingResponse(
        SsoRoleMapping entity,
        IReadOnlyDictionary<Guid, Role> rolesById)
    {
        rolesById.TryGetValue(entity.LocalRoleId, out var role);
        return new SsoRoleMappingResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ProviderId = entity.ProviderId,
            ExternalRole = entity.ExternalRole,
            LocalRoleId = entity.LocalRoleId,
            LocalRoleCode = role?.Code,
            LocalRoleName = role?.Name
        };
    }

    private static SsoDepartmentMappingResponse ToDepartmentMappingResponse(
        SsoDepartmentMapping entity,
        IReadOnlyDictionary<Guid, Department> departmentsById)
    {
        departmentsById.TryGetValue(entity.LocalDepartmentId, out var department);
        return new SsoDepartmentMappingResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ProviderId = entity.ProviderId,
            ExternalDepartment = entity.ExternalDepartment,
            LocalDepartmentId = entity.LocalDepartmentId,
            LocalDepartmentCode = department?.Code,
            LocalDepartmentName = department?.Name
        };
    }

    private static SsoLoginLogResponse ToLoginLogResponse(SsoLoginLog entity)
    {
        return new SsoLoginLogResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ProviderCode = entity.ProviderCode,
            ProviderName = entity.ProviderName,
            ProviderType = entity.ProviderType,
            ExternalUserId = entity.ExternalUserId,
            ExternalUserName = entity.ExternalUserName,
            LocalUserId = entity.LocalUserId,
            LocalUserName = entity.LocalUserName,
            LoginResult = entity.LoginResult,
            FailureReason = entity.FailureReason,
            IpAddress = entity.IpAddress,
            UserAgent = entity.UserAgent,
            TraceId = entity.TraceId,
            CreatedAt = entity.CreatedAt
        };
    }

    private static bool IsSuperAdminRole(Role role)
    {
        return string.Equals(role.Code, SystemBuiltinConstants.SuperAdminRoleCode, StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }
}
