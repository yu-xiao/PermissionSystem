using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Permissions;

public sealed class PermissionService : IPermissionService
{
    private readonly IRepository<Domain.Entities.Permission> _permissionRepository;
    private readonly IRepository<Domain.Entities.RolePermission> _rolePermissionRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<User> _userRepository;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IUnitOfWork _unitOfWork;

    public PermissionService(
        IRepository<Domain.Entities.Permission> permissionRepository,
        IRepository<Domain.Entities.RolePermission> rolePermissionRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<User> userRepository,
        ITenantWriteResolver tenantWriteResolver,
        IUnitOfWork unitOfWork)
    {
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _userRoleRepository = userRoleRepository;
        _userRepository = userRepository;
        _tenantWriteResolver = tenantWriteResolver;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<PermissionResponse>> GetPagedAsync(PermissionQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = _permissionRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.Code.Contains(keyword) ||
                entity.Name.Contains(keyword) ||
                entity.Group.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.Group))
        {
            var group = request.Group.Trim();
            query = query.Where(entity => entity.Group == group);
        }

        var totalCount = query.LongCount();
        var permissions = query
            .OrderBy(entity => entity.Group)
            .ThenBy(entity => entity.Code)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<PermissionResponse>.Create(permissions, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<PermissionResponse> CreateAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Code, "Permission code is required.");
        ValidateRequired(request.Name, "Permission name is required.");
        ValidateRequired(request.Group, "Permission group is required.");

        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        var code = request.Code.Trim();
        if (_permissionRepository.Query().Any(entity => entity.TenantId == tenantId && entity.Code == code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Permission code already exists.");
        }

        var permission = new Domain.Entities.Permission
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            Group = request.Group.Trim(),
            Description = request.Description,
            Resource = request.Resource,
            Action = request.Action
        };

        await _permissionRepository.AddAsync(permission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(permission);
    }

    public async Task<PermissionResponse> UpdateAsync(Guid id, UpdatePermissionRequest request, CancellationToken cancellationToken = default)
    {
        var permission = await GetPermissionOrThrowAsync(id, cancellationToken);
        permission.Name = request.Name.Trim();
        permission.Group = request.Group.Trim();
        permission.Description = request.Description;
        permission.Resource = request.Resource;
        permission.Action = request.Action;

        _permissionRepository.Update(permission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(permission);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var permission = await GetPermissionOrThrowAsync(id, cancellationToken);
        var roleIds = _rolePermissionRepository.Query()
            .Where(entity => entity.TenantId == permission.TenantId && entity.PermissionId == id)
            .Select(entity => entity.RoleId)
            .Distinct()
            .ToArray();
        var userIds = _userRoleRepository.Query()
            .Where(entity => entity.TenantId == permission.TenantId && roleIds.Contains(entity.RoleId))
            .Select(entity => entity.UserId)
            .Distinct()
            .ToArray();

        foreach (var relation in _rolePermissionRepository.Query()
                     .Where(entity => entity.TenantId == permission.TenantId && entity.PermissionId == id)
                     .ToList())
        {
            _rolePermissionRepository.Remove(relation);
        }

        _permissionRepository.Remove(permission);

        foreach (var user in _userRepository.Query()
                     .Where(entity => entity.TenantId == permission.TenantId && userIds.Contains(entity.Id))
                     .ToList())
        {
            user.RotateSecurityStamp();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Domain.Entities.Permission> GetPermissionOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _permissionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Permission was not found.");
    }

    private static PermissionResponse ToResponse(Domain.Entities.Permission permission)
    {
        return new PermissionResponse
        {
            Id = permission.Id,
            TenantId = permission.TenantId,
            Code = permission.Code,
            Name = permission.Name,
            Group = permission.Group,
            Description = permission.Description,
            Resource = permission.Resource,
            Action = permission.Action,
            CreatedAt = permission.CreatedAt
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
