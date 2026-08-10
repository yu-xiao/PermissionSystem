using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Departments;

public sealed class DepartmentService : IDepartmentService
{
    private const string EnabledStatus = "Enabled";
    private const string DisabledStatus = "Disabled";

    private readonly IRepository<Department> _departmentRepository;
    private readonly IRepository<User> _userRepository;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IUnitOfWork _unitOfWork;

    public DepartmentService(
        IRepository<Department> departmentRepository,
        IRepository<User> userRepository,
        ITenantWriteResolver tenantWriteResolver,
        IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _userRepository = userRepository;
        _tenantWriteResolver = tenantWriteResolver;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<DepartmentTreeResponse>> GetTreeAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var departments = _departmentRepository.Query()
            .Where(entity => !tenantId.HasValue || entity.TenantId == tenantId.Value)
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.Code)
            .ToList();

        return Task.FromResult(BuildTree(departments));
    }

    public async Task<DepartmentTreeResponse> CreateAsync(
        CreateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Code, "Department code is required.");
        ValidateRequired(request.Name, "Department name is required.");

        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);
        var code = request.Code.Trim();
        if (_departmentRepository.Query().Any(entity => entity.TenantId == tenantId && entity.Code == code))
        {
            throw new BusinessException(ErrorCode.Conflict, "Department code already exists.");
        }

        var department = new Department
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ParentId = request.ParentId,
            Code = code,
            Name = request.Name.Trim(),
            Sort = request.Sort,
            Status = NormalizeStatus(request.Status),
            IsEnabled = IsEnabledStatus(request.Status)
        };

        department.TreePath = await BuildTreePathAsync(department.Id, department.ParentId, department.TenantId, cancellationToken);

        await _departmentRepository.AddAsync(department, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToTreeResponse(department);
    }

    public async Task<DepartmentTreeResponse> UpdateAsync(
        Guid id,
        UpdateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Name, "Department name is required.");

        var department = await GetDepartmentOrThrowAsync(id, cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(department, request.ConcurrencyToken);
        if (request.ParentId == id)
        {
            throw new BusinessException(ErrorCode.BadRequest, "Department cannot be its own parent.");
        }

        if (request.ParentId.HasValue && IsDescendant(id, request.ParentId.Value))
        {
            throw new BusinessException(ErrorCode.BadRequest, "Department cannot use its child as parent.");
        }

        department.ParentId = request.ParentId;
        department.Name = request.Name.Trim();
        department.Sort = request.Sort;
        department.Status = NormalizeStatus(request.Status);
        department.IsEnabled = IsEnabledStatus(request.Status);
        department.TreePath = await BuildTreePathAsync(department.Id, department.ParentId, department.TenantId, cancellationToken);

        UpdateChildrenTreePath(department);

        _departmentRepository.Update(department);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToTreeResponse(department);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var department = await GetDepartmentOrThrowAsync(id, cancellationToken);
        if (_departmentRepository.Query().Any(entity => entity.ParentId == id))
        {
            throw new BusinessException(ErrorCode.Conflict, "Please delete child departments first.");
        }

        if (_userRepository.Query().Any(entity => entity.DepartmentId == id))
        {
            throw new BusinessException(ErrorCode.Conflict, "Department has users and cannot be deleted.");
        }

        _departmentRepository.Remove(department);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(
        Guid id,
        SetDepartmentEnabledRequest request,
        CancellationToken cancellationToken = default)
    {
        var department = await GetDepartmentOrThrowAsync(id, cancellationToken);
        department.IsEnabled = request.IsEnabled;
        department.Status = request.IsEnabled ? EnabledStatus : DisabledStatus;

        _departmentRepository.Update(department);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> BuildTreePathAsync(
        Guid id,
        Guid? parentId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!parentId.HasValue)
        {
            return $"/{id}/";
        }

        var parent = await _departmentRepository.GetByIdAsync(parentId.Value, cancellationToken)
            ?? throw new BusinessException(ErrorCode.BadRequest, "Parent department is invalid.");

        if (parent.TenantId != tenantId)
        {
            throw new BusinessException(ErrorCode.BadRequest, "Parent department is invalid.");
        }

        return $"{parent.TreePath}{id}/";
    }

    private bool IsDescendant(Guid id, Guid parentId)
    {
        var current = _departmentRepository.GetByIdAsync(parentId).GetAwaiter().GetResult();
        return current?.TreePath.Contains($"/{id}/", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void UpdateChildrenTreePath(Department parent)
    {
        var children = _departmentRepository.Query()
            .Where(entity => entity.ParentId == parent.Id)
            .ToList();

        foreach (var child in children)
        {
            child.TreePath = $"{parent.TreePath}{child.Id}/";
            UpdateChildrenTreePath(child);
        }
    }

    private async Task<Department> GetDepartmentOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _departmentRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Department was not found.");
    }

    private static IReadOnlyList<DepartmentTreeResponse> BuildTree(IReadOnlyCollection<Department> departments)
    {
        return departments
            .Where(entity => entity.ParentId is null)
            .OrderBy(entity => entity.Sort)
            .Select(entity => BuildNode(entity, departments))
            .ToList();
    }

    private static DepartmentTreeResponse BuildNode(Department department, IReadOnlyCollection<Department> departments)
    {
        var children = departments
            .Where(entity => entity.ParentId == department.Id)
            .OrderBy(entity => entity.Sort)
            .Select(entity => BuildNode(entity, departments))
            .ToList();

        return ToTreeResponse(department, children);
    }

    private static DepartmentTreeResponse ToTreeResponse(
        Department department,
        IReadOnlyList<DepartmentTreeResponse>? children = null)
    {
        return new DepartmentTreeResponse
        {
            Id = department.Id,
            TenantId = department.TenantId,
            ParentId = department.ParentId,
            Code = department.Code,
            Name = department.Name,
            TreePath = department.TreePath,
            Sort = department.Sort,
            Status = department.Status,
            IsEnabled = department.IsEnabled,
            ConcurrencyToken = department.RowVersion,
            Children = children ?? []
        };
    }

    private static string NormalizeStatus(string status)
    {
        return IsEnabledStatus(status) ? EnabledStatus : DisabledStatus;
    }

    private static bool IsEnabledStatus(string status)
    {
        return !string.Equals(status, DisabledStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }
}
