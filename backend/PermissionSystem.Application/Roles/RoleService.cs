using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Roles;

public sealed class RoleService : IRoleService
{
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<RoleMenu> _roleMenuRepository;
    private readonly IRepository<RolePermission> _rolePermissionRepository;
    private readonly IRepository<Menu> _menuRepository;
    private readonly IRepository<Domain.Entities.Permission> _permissionRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RoleService(
        IRepository<Role> roleRepository,
        IRepository<RoleMenu> roleMenuRepository,
        IRepository<RolePermission> rolePermissionRepository,
        IRepository<Menu> menuRepository,
        IRepository<Domain.Entities.Permission> permissionRepository,
        IRepository<UserRole> userRoleRepository,
        IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _roleMenuRepository = roleMenuRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _menuRepository = menuRepository;
        _permissionRepository = permissionRepository;
        _userRoleRepository = userRoleRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<RoleResponse>> GetPagedAsync(RoleQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = _roleRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity => entity.Code.Contains(keyword) || entity.Name.Contains(keyword));
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var roles = query
            .OrderBy(entity => entity.Sort)
            .ThenByDescending(entity => entity.CreatedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<RoleResponse>.Create(roles, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Code, "Role code is required.");
        ValidateRequired(request.Name, "Role name is required.");

        var code = request.Code.Trim();
        if (_roleRepository.Query().Any(entity => entity.TenantId == request.TenantId && entity.Code == code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Role code already exists.");
        }

        var role = new Role
        {
            TenantId = request.TenantId,
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description,
            IsEnabled = request.IsEnabled,
            Sort = request.Sort
        };

        await _roleRepository.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(role);
    }

    public async Task<RoleResponse> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(id, cancellationToken);

        role.Name = request.Name.Trim();
        role.Description = request.Description;
        role.IsEnabled = request.IsEnabled;
        role.Sort = request.Sort;

        _roleRepository.Update(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(role);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(id, cancellationToken);

        foreach (var relation in _userRoleRepository.Query().Where(entity => entity.RoleId == id).ToList())
        {
            _userRoleRepository.Remove(relation);
        }

        foreach (var relation in _roleMenuRepository.Query().Where(entity => entity.RoleId == id).ToList())
        {
            _roleMenuRepository.Remove(relation);
        }

        foreach (var relation in _rolePermissionRepository.Query().Where(entity => entity.RoleId == id).ToList())
        {
            _rolePermissionRepository.Remove(relation);
        }

        _roleRepository.Remove(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignMenusAsync(Guid id, AssignRoleMenusRequest request, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(id, cancellationToken);
        var menuIds = request.MenuIds.Distinct().ToArray();
        var validMenuIds = _menuRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && menuIds.Contains(entity.Id))
            .Select(entity => entity.Id)
            .ToArray();

        if (validMenuIds.Length != menuIds.Length)
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more menus are invalid.");
        }

        foreach (var relation in _roleMenuRepository.Query().Where(entity => entity.RoleId == id).ToList())
        {
            _roleMenuRepository.Remove(relation);
        }

        foreach (var menuId in validMenuIds)
        {
            await _roleMenuRepository.AddAsync(new RoleMenu
            {
                TenantId = role.TenantId,
                RoleId = role.Id,
                MenuId = menuId
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignPermissionsAsync(Guid id, AssignRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(id, cancellationToken);
        var permissionIds = request.PermissionIds.Distinct().ToArray();
        var validPermissionIds = _permissionRepository.Query()
            .Where(entity => entity.TenantId == role.TenantId && permissionIds.Contains(entity.Id))
            .Select(entity => entity.Id)
            .ToArray();

        if (validPermissionIds.Length != permissionIds.Length)
        {
            throw new BusinessException(ErrorCode.BadRequest, "One or more permissions are invalid.");
        }

        foreach (var relation in _rolePermissionRepository.Query().Where(entity => entity.RoleId == id).ToList())
        {
            _rolePermissionRepository.Remove(relation);
        }

        foreach (var permissionId in validPermissionIds)
        {
            await _rolePermissionRepository.AddAsync(new RolePermission
            {
                TenantId = role.TenantId,
                RoleId = role.Id,
                PermissionId = permissionId
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Role> GetRoleOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _roleRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Role was not found.");
    }

    private static RoleResponse ToResponse(Role role)
    {
        return new RoleResponse
        {
            Id = role.Id,
            TenantId = role.TenantId,
            Code = role.Code,
            Name = role.Name,
            Description = role.Description,
            IsEnabled = role.IsEnabled,
            Sort = role.Sort,
            CreatedAt = role.CreatedAt
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
