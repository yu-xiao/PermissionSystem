namespace PermissionSystem.Application.Departments;

public sealed class CreateDepartmentRequest
{
    public Guid TenantId { get; init; }

    public Guid? ParentId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Sort { get; init; }

    public string Status { get; init; } = "Enabled";
}

public sealed class UpdateDepartmentRequest
{
    public Guid? ParentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Sort { get; init; }

    public string Status { get; init; } = "Enabled";
}

public sealed class SetDepartmentEnabledRequest
{
    public bool IsEnabled { get; init; }
}

public sealed class DepartmentTreeResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid? ParentId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string TreePath { get; init; } = string.Empty;

    public int Sort { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<DepartmentTreeResponse> Children { get; init; } = [];
}

public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentTreeResponse>> GetTreeAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);

    Task<DepartmentTreeResponse> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken = default);

    Task<DepartmentTreeResponse> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(Guid id, SetDepartmentEnabledRequest request, CancellationToken cancellationToken = default);
}
