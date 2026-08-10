using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.DataPermissions;

public sealed class DataScopeService : IDataScopeService, IUserDataScopeService
{
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<RoleDataScope> _roleDataScopeRepository;
    private readonly IRepository<UserDataScope>? _userDataScopeRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DataScopeService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public DataScopeService(
        IRepository<Role> roleRepository,
        IRepository<User> userRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<RoleDataScope> roleDataScopeRepository,
        IRepository<UserDataScope>? userDataScopeRepository,
        IRepository<Department> departmentRepository,
        ICurrentUserService currentUserService,
        ILogger<DataScopeService> logger,
        IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleDataScopeRepository = roleDataScopeRepository;
        _userDataScopeRepository = userDataScopeRepository;
        _departmentRepository = departmentRepository;
        _currentUserService = currentUserService;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public DataScopeService(
        IRepository<Role> roleRepository,
        IRepository<User> userRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<RoleDataScope> roleDataScopeRepository,
        IRepository<Department> departmentRepository,
        ICurrentUserService currentUserService,
        ILogger<DataScopeService> logger,
        IUnitOfWork unitOfWork)
        : this(
            roleRepository,
            userRepository,
            userRoleRepository,
            roleDataScopeRepository,
            null,
            departmentRepository,
            currentUserService,
            logger,
            unitOfWork)
    {
    }

    public async Task<DataScopeContext> GetCurrentUserDataScopeAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUserService.IsSuperAdmin)
        {
            return new DataScopeContext
            {
                ScopeType = DataScopeType.All,
                CurrentUserId = _currentUserService.UserId,
                CurrentDepartmentId = _currentUserService.DepartmentId
            };
        }

        if (!_currentUserService.UserId.HasValue)
        {
            return CreateCurrentUserScope();
        }

        var tenantId = _currentUserService.TenantId;
        var userScope = _userDataScopeRepository?.Query()
            .FirstOrDefault(entity =>
                entity.UserId == _currentUserService.UserId.Value &&
                (!tenantId.HasValue || entity.TenantId == tenantId.Value));
        if (userScope is not null)
        {
            return await ResolveSingleScopeAsync(
                userScope.ScopeType,
                userScope.CustomDepartmentIds,
                cancellationToken);
        }

        var assignedRoleIds = _userRoleRepository.Query()
            .Where(entity => entity.UserId == _currentUserService.UserId.Value &&
                (!tenantId.HasValue || entity.TenantId == tenantId.Value))
            .Select(entity => entity.RoleId)
            .ToArray();
        var roleIds = _roleRepository.Query()
            .Where(entity =>
                assignedRoleIds.Contains(entity.Id) &&
                entity.IsEnabled &&
                (!tenantId.HasValue || entity.TenantId == tenantId.Value))
            .Select(entity => entity.Id)
            .ToArray();

        var roleScopes = _roleDataScopeRepository.Query()
            .Where(entity => roleIds.Contains(entity.RoleId))
            .ToList();

        if (roleScopes.Count == 0)
        {
            return CreateCurrentUserScope();
        }

        if (roleScopes.Any(entity => entity.ScopeType == DataScopeType.All))
        {
            return new DataScopeContext
            {
                ScopeType = DataScopeType.All,
                CurrentUserId = _currentUserService.UserId,
                CurrentDepartmentId = _currentUserService.DepartmentId
            };
        }

        var departmentIds = new HashSet<Guid>();
        var includeCurrentUser = false;
        IReadOnlyCollection<Guid>? currentDepartmentAndChildrenIds = null;

        foreach (var roleScope in roleScopes)
        {
            switch (roleScope.ScopeType)
            {
                case DataScopeType.CurrentUser:
                    includeCurrentUser = true;
                    break;
                case DataScopeType.CurrentDepartment:
                    if (_currentUserService.DepartmentId.HasValue)
                    {
                        departmentIds.Add(_currentUserService.DepartmentId.Value);
                    }
                    break;
                case DataScopeType.CurrentDepartmentAndChildren:
                    currentDepartmentAndChildrenIds ??=
                        await GetCurrentDepartmentAndChildrenIdsAsync(cancellationToken);
                    foreach (var departmentId in currentDepartmentAndChildrenIds)
                    {
                        departmentIds.Add(departmentId);
                    }
                    break;
                case DataScopeType.CustomDepartments:
                    foreach (var departmentId in DeserializeDepartmentIds(roleScope.CustomDepartmentIds))
                    {
                        departmentIds.Add(departmentId);
                    }
                    break;
            }
        }

        return new DataScopeContext
        {
            ScopeType = departmentIds.Count > 0
                ? DataScopeType.CustomDepartments
                : includeCurrentUser
                    ? DataScopeType.CurrentUser
                    : DataScopeType.CustomDepartments,
            CurrentUserId = _currentUserService.UserId,
            CurrentDepartmentId = _currentUserService.DepartmentId,
            DepartmentIds = departmentIds.ToArray(),
            IncludeCurrentUser = includeCurrentUser
        };
    }

    private async Task<DataScopeContext> ResolveSingleScopeAsync(
        DataScopeType scopeType,
        string? customDepartmentIds,
        CancellationToken cancellationToken)
    {
        if (scopeType == DataScopeType.All)
        {
            return new DataScopeContext
            {
                ScopeType = DataScopeType.All,
                CurrentUserId = _currentUserService.UserId,
                CurrentDepartmentId = _currentUserService.DepartmentId
            };
        }

        IReadOnlyCollection<Guid> departmentIds = scopeType switch
        {
            DataScopeType.CurrentDepartment when _currentUserService.DepartmentId.HasValue =>
                [_currentUserService.DepartmentId.Value],
            DataScopeType.CurrentDepartmentAndChildren =>
                await GetCurrentDepartmentAndChildrenIdsAsync(cancellationToken),
            DataScopeType.CustomDepartments => DeserializeDepartmentIds(customDepartmentIds),
            _ => []
        };

        return new DataScopeContext
        {
            ScopeType = scopeType,
            CurrentUserId = _currentUserService.UserId,
            CurrentDepartmentId = _currentUserService.DepartmentId,
            DepartmentIds = departmentIds,
            IncludeCurrentUser = scopeType == DataScopeType.CurrentUser
        };
    }

    private DataScopeContext CreateCurrentUserScope()
    {
        return new DataScopeContext
        {
            ScopeType = DataScopeType.CurrentUser,
            CurrentUserId = _currentUserService.UserId,
            CurrentDepartmentId = _currentUserService.DepartmentId,
            IncludeCurrentUser = true
        };
    }

    public async Task<RoleDataScopeResponse> GetRoleDataScopeAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(roleId, cancellationToken);
        var dataScope = _roleDataScopeRepository.Query().FirstOrDefault(entity => entity.RoleId == role.Id);

        return new RoleDataScopeResponse
        {
            RoleId = role.Id,
            ScopeType = dataScope?.ScopeType ?? GetDefaultScopeType(role),
            DepartmentIds = DeserializeDepartmentIds(dataScope?.CustomDepartmentIds)
        };
    }

    public async Task SetRoleDataScopeAsync(
        Guid roleId,
        SetRoleDataScopeRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(roleId, cancellationToken);
        EnsureCanSetRoleDataScope(role, request);
        var departmentIds = request.ScopeType == DataScopeType.CustomDepartments
            ? request.DepartmentIds.Distinct().ToArray()
            : Array.Empty<Guid>();

        if (request.ScopeType == DataScopeType.CustomDepartments)
        {
            var validDepartmentCount = _departmentRepository.Query()
                .Count(entity => entity.TenantId == role.TenantId && departmentIds.Contains(entity.Id));

            if (validDepartmentCount != departmentIds.Length)
            {
                throw new BusinessException(ErrorCode.BadRequest, "One or more departments are invalid.");
            }
        }

        var dataScope = _roleDataScopeRepository.Query().FirstOrDefault(entity => entity.RoleId == role.Id);
        if (dataScope is null)
        {
            dataScope = new RoleDataScope
            {
                TenantId = role.TenantId,
                RoleId = role.Id
            };
            await _roleDataScopeRepository.AddAsync(dataScope, cancellationToken);
        }

        dataScope.ScopeType = request.ScopeType;
        dataScope.CustomDepartmentIds = departmentIds.Length > 0 ? JsonSerializer.Serialize(departmentIds) : null;

        var affectedUserIds = _userRoleRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && entity.RoleId == role.Id)
            .Select(entity => entity.UserId)
            .Distinct()
            .ToArray();
        foreach (var user in _userRepository.Query()
                     .Where(entity => entity.TenantId == role.TenantId && affectedUserIds.Contains(entity.Id))
                     .ToList())
        {
            user.RotateSecurityStamp();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDataScopeResponse> GetUserDataScopeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var userDataScope = RequireUserDataScopeRepository().Query()
            .FirstOrDefault(entity => entity.UserId == user.Id);

        return new UserDataScopeResponse
        {
            UserId = user.Id,
            HasOverride = userDataScope is not null,
            ScopeType = userDataScope?.ScopeType ?? DataScopeType.CurrentUser,
            DepartmentIds = DeserializeDepartmentIds(userDataScope?.CustomDepartmentIds)
        };
    }

    public async Task SetUserDataScopeAsync(
        Guid userId,
        SetUserDataScopeRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        EnsureValidScopeType(request.ScopeType);
        var departmentIds = request.ScopeType == DataScopeType.CustomDepartments
            ? request.DepartmentIds.Distinct().ToArray()
            : [];

        if (request.ScopeType == DataScopeType.CustomDepartments)
        {
            var validDepartmentCount = _departmentRepository.Query()
                .Count(entity => entity.TenantId == user.TenantId && departmentIds.Contains(entity.Id));
            if (validDepartmentCount != departmentIds.Length)
            {
                throw new BusinessException(ErrorCode.BadRequest, "One or more departments are invalid.");
            }
        }

        var repository = RequireUserDataScopeRepository();
        var userDataScope = repository.Query().FirstOrDefault(entity => entity.UserId == user.Id);
        if (userDataScope is null)
        {
            userDataScope = new UserDataScope
            {
                TenantId = user.TenantId,
                UserId = user.Id
            };
            await repository.AddAsync(userDataScope, cancellationToken);
        }

        userDataScope.ScopeType = request.ScopeType;
        userDataScope.CustomDepartmentIds = departmentIds.Length > 0
            ? JsonSerializer.Serialize(departmentIds)
            : null;
        user.RotateSecurityStamp();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearUserDataScopeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var repository = RequireUserDataScopeRepository();
        var userDataScope = repository.Query().FirstOrDefault(entity => entity.UserId == user.Id);
        if (userDataScope is null)
        {
            return;
        }

        repository.Remove(userDataScope);
        user.RotateSecurityStamp();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<Guid>> GetCurrentDepartmentAndChildrenIdsAsync(CancellationToken cancellationToken)
    {
        if (!_currentUserService.DepartmentId.HasValue)
        {
            return [];
        }

        var department = await _departmentRepository.GetByIdAsync(_currentUserService.DepartmentId.Value, cancellationToken);
        if (department is null)
        {
            return [];
        }

        return _departmentRepository.Query()
            .Where(entity => entity.Id == department.Id || entity.TreePath.StartsWith(department.TreePath))
            .Select(entity => entity.Id)
            .ToArray();
    }

    private async Task<Role> GetRoleOrThrowAsync(Guid roleId, CancellationToken cancellationToken)
    {
        return await _roleRepository.GetByIdAsync(roleId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Role was not found.");
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "User was not found.");
    }

    private IRepository<UserDataScope> RequireUserDataScopeRepository()
    {
        return _userDataScopeRepository
            ?? throw new InvalidOperationException("User data scope repository is not configured.");
    }

    private static void EnsureValidScopeType(DataScopeType scopeType)
    {
        if (!Enum.IsDefined(scopeType))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Data scope type is invalid.");
        }
    }

    private static DataScopeType GetDefaultScopeType(Role role)
    {
        return string.Equals(role.Code, ClaimConstants.SuperAdminRoleCode, StringComparison.OrdinalIgnoreCase)
            ? DataScopeType.All
            : DataScopeType.CurrentUser;
    }

    private void EnsureCanSetRoleDataScope(Role role, SetRoleDataScopeRequest request)
    {
        if (!IsProtectedRole(role))
        {
            return;
        }

        if (!_currentUserService.IsSuperAdmin)
        {
            _logger.LogWarning(
                "Blocked non-SuperAdmin modifying protected role data scope {RoleId}. Actor {UserId}.",
                role.Id,
                _currentUserService.UserId);
            throw new BusinessException(ErrorCode.Forbidden, "无权修改超级管理员角色数据范围。");
        }

        if (request.ScopeType != DataScopeType.All || request.DepartmentIds.Count > 0)
        {
            _logger.LogWarning(
                "Blocked shrinking protected role data scope {RoleId}. Actor {UserId}.",
                role.Id,
                _currentUserService.UserId);
            throw new BusinessException(ErrorCode.Forbidden, "超级管理员角色数据范围必须保持全部数据。");
        }
    }

    private static bool IsProtectedRole(Role role)
    {
        return role.IsBuiltin ||
            string.Equals(role.Code, SystemBuiltinConstants.SuperAdminRoleCode, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<Guid> DeserializeDepartmentIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Guid[]>(value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
