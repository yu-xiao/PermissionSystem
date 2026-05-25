using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.Roles;

public sealed class SaveRolePermissionMatrixRequest
{
    public IReadOnlyCollection<Guid> MenuIds { get; init; } = [];

    public IReadOnlyCollection<Guid> PermissionIds { get; init; } = [];

    public IReadOnlyCollection<RoleMenuDataScopeRequest> DataScopes { get; init; } = [];

    public IReadOnlyCollection<RoleFieldPermissionRequest> FieldPermissions { get; init; } = [];
}

public sealed class RoleMenuDataScopeRequest
{
    public Guid MenuId { get; init; }

    public DataScopeType ScopeType { get; init; }

    public IReadOnlyCollection<Guid> DepartmentIds { get; init; } = [];
}

public sealed class RoleFieldPermissionRequest
{
    public Guid MenuId { get; init; }

    public string FieldCode { get; init; } = string.Empty;

    public bool Visible { get; init; }

    public bool Editable { get; init; }

    public bool Masked { get; init; }
}

public sealed class RolePermissionMatrixResponse
{
    public Guid RoleId { get; init; }

    public string RoleName { get; init; } = string.Empty;

    public IReadOnlyList<PermissionModuleResponse> Modules { get; init; } = [];
}

public sealed class PermissionModuleResponse
{
    public Guid ModuleId { get; init; }

    public string ModuleName { get; init; } = string.Empty;

    public string? ModuleCode { get; init; }

    public int Sort { get; init; }

    public bool Checked { get; init; }

    public bool Indeterminate { get; init; }

    public bool Expanded { get; init; } = true;

    public IReadOnlyList<PermissionMenuRowResponse> Menus { get; init; } = [];
}

public sealed class PermissionMenuRowResponse
{
    public Guid MenuId { get; init; }

    public Guid? ParentId { get; init; }

    public string MenuName { get; init; } = string.Empty;

    public string? MenuPath { get; init; }

    public string? MenuCode { get; init; }

    public string? Icon { get; init; }

    public int Sort { get; init; }

    public bool Checked { get; init; }

    public bool Indeterminate { get; init; }

    public IReadOnlyList<PermissionItemResponse> Permissions { get; init; } = [];

    public bool DataScopeEnabled { get; init; }

    public bool FieldPermissionEnabled { get; init; }

    public string? DataScopeSummary { get; init; }

    public string? FieldPermissionSummary { get; init; }
}

public sealed class PermissionItemResponse
{
    public Guid PermissionId { get; init; }

    public string PermissionName { get; init; } = string.Empty;

    public string PermissionCode { get; init; } = string.Empty;

    public string PermissionType { get; init; } = string.Empty;

    public int Sort { get; init; }

    public bool Checked { get; init; }
}
